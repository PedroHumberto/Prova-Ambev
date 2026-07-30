using System.Text.Json;
using Ambev.DeveloperEvaluation.Application.Common.Exceptions;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
            NullLogger<GlobalExceptionHandler>.Instance);

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
    public async Task TryHandleAsync_RequestCancellation_DoesNotHandleOrWriteResponse()
    {
        await using var provider = CreateServices();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var context = CreateHttpContext(provider);
        context.RequestAborted = cancellationSource.Token;
        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(cancellationSource.Token),
            cancellationSource.Token);

        handled.Should().BeFalse();
        context.Response.Body.Length.Should().Be(0);
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
