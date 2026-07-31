using System.Text.Json;
using Ambev.DeveloperEvaluation.Application.Common.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.Unit.TestInfrastructure;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string, string> ExceptionCases => new()
    {
        {
            new ValidationException([new ValidationFailure("Email", "Email is required.")]),
            StatusCodes.Status400BadRequest,
            "Validation failed",
            "One or more validation errors occurred."
        },
        {
            new UnauthorizedAccessException("Sensitive authentication detail"),
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication failed."
        },
        {
            new KeyNotFoundException("User was not found."),
            StatusCodes.Status404NotFound,
            "Not Found",
            "User was not found."
        },
        {
            new UserAlreadyExistsException("test.user@example.com"),
            StatusCodes.Status409Conflict,
            "Conflict",
            "User with email test.user@example.com already exists"
        },
        {
            new DuplicateUserEmailException("concurrent.user@example.com"),
            StatusCodes.Status409Conflict,
            "Conflict",
            "User with email concurrent.user@example.com already exists"
        },
        {
            new DuplicateSaleNumberException("SALE-001"),
            StatusCodes.Status409Conflict,
            "Conflict",
            "A sale with number 'SALE-001' already exists."
        },
        {
            new SaleItemNotFoundException(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            StatusCodes.Status404NotFound,
            "Not Found",
            "Sale item 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' does not belong to the sale."
        },
        {
            new LastActiveSaleItemException(),
            StatusCodes.Status422UnprocessableEntity,
            "Unprocessable Entity",
            "The last active sale item cannot be cancelled. Cancel the sale instead."
        },
        {
            new InvalidSaleException("Invalid aggregate state."),
            StatusCodes.Status422UnprocessableEntity,
            "Unprocessable Entity",
            "Invalid aggregate state."
        },
        {
            new InvalidOperationException("Sensitive internal detail"),
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred."
        }
    };

    [Theory]
    [MemberData(nameof(ExceptionCases))]
    public async Task TryHandleAsync_MappedException_WritesSafeProblemDetails(
        Exception exception,
        int expectedStatus,
        string expectedTitle,
        string expectedDetail)
    {
        await using var provider = CreateServices();
        var context = CreateHttpContext(provider);
        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            new RecordingLogger<GlobalExceptionHandler>());

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        var body = await ReadBody(context.Response);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        root.GetProperty("title").GetString().Should().Be(expectedTitle);
        root.GetProperty("detail").GetString().Should().Be(expectedDetail);
        root.GetProperty("instance").GetString().Should().Be("/users");
        body.ToLowerInvariant().Should().NotContain(nameof(Exception.StackTrace).ToLowerInvariant());
        body.Should().NotContain(" at ");

        if (exception is ValidationException)
            root.GetProperty("errors").GetProperty("Email")[0].GetString().Should().Be("Email is required.");
        if (expectedStatus == StatusCodes.Status500InternalServerError)
            body.Should().NotContain(exception.Message);
        if (expectedStatus == StatusCodes.Status401Unauthorized)
            body.Should().NotContain(exception.Message);
    }

    [Fact]
    public async Task TryHandleAsync_ExpectedFailure_LogsStructuredWarningWithoutExceptionOrSensitiveMessage()
    {
        const string sensitiveMessage = "token-sentinel-do-not-log";
        await using var provider = CreateServices();
        var context = CreateHttpContext(provider);
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            logger);

        await handler.TryHandleAsync(
            context,
            new UnauthorizedAccessException(sensitiveMessage),
            CancellationToken.None);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeNull();
        entry.Properties["StatusCode"].Should().Be(StatusCodes.Status401Unauthorized);
        entry.Properties["ExceptionType"].Should().Be(typeof(UnauthorizedAccessException).FullName);
        entry.Properties["RequestMethod"].Should().Be("GET");
        entry.Properties["RequestPath"].Should().BeEquivalentTo(context.Request.Path);
        entry.Message.Should().NotContain(sensitiveMessage);
        entry.Properties.Values.Select(value => value?.ToString()).Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedFailure_LogsStructuredErrorWithOriginalException()
    {
        await using var provider = CreateServices();
        var context = CreateHttpContext(provider);
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            logger);
        var exception = new InvalidOperationException("Sensitive internal detail");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Properties["StatusCode"].Should().Be(StatusCodes.Status500InternalServerError);
        entry.Properties["RequestMethod"].Should().Be("GET");
        entry.Properties["RequestPath"].Should().BeEquivalentTo(context.Request.Path);
    }

    [Fact]
    public async Task TryHandleAsync_RequestCancellation_DoesNotHandleWriteOrLog()
    {
        await using var provider = CreateServices();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var context = CreateHttpContext(provider);
        context.RequestAborted = cancellationSource.Token;
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            logger);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(cancellationSource.Token),
            cancellationSource.Token);

        handled.Should().BeFalse();
        context.Response.Body.Length.Should().Be(0);
        logger.Entries.Should().BeEmpty();
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddProblemDetails();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider provider)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Path = "/users";
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBody(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
