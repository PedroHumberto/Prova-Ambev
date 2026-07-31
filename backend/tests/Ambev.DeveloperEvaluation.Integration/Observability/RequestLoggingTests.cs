using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Common.Logging;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Observability;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RequestLoggingCollection
{
    public const string Name = "Request logging tests";
}

[Collection(RequestLoggingCollection.Name)]
public sealed class RequestLoggingTests
{
    private const string CorrelationId = "task-012.request";
    private const string AuthorizationSentinel = "Bearer token-sentinel-do-not-log";
    private const string BodySentinel = "password-sentinel-do-not-log";

    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(400, LogEventLevel.Warning)]
    [InlineData(500, LogEventLevel.Error)]
    public async Task RequestCompletion_StatusCode_EmitsStructuredSafeLog(
        int statusCode,
        LogEventLevel expectedLevel)
    {
        var sink = new CollectingSink();
        await using var app = CreateApplication(sink);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/status/{statusCode}")
        {
            Content = new StringContent(BodySentinel, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", AuthorizationSentinel);
        request.Headers.Add(RequestCorrelationMiddleware.CorrelationHeaderName, CorrelationId);

        using var response = await client.SendAsync(request);

        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
        Assert.Equal(CorrelationId, response.Headers.GetValues(RequestCorrelationMiddleware.CorrelationHeaderName).Single());
        var completion = Assert.Single(sink.Events, IsRequestCompletion);
        Assert.Equal(expectedLevel, completion.Level);
        Assert.Equal(statusCode, completion.Properties["StatusCode"].LiteralValue());
        Assert.True(Convert.ToDouble(completion.Properties["Elapsed"].LiteralValue()) >= 0);
        Assert.Equal(CorrelationId, completion.Properties["CorrelationId"].LiteralValue());
        Assert.False(string.IsNullOrWhiteSpace(completion.Properties["TraceId"].LiteralValue()?.ToString()));
        var serializedLog = completion.RenderMessage() + string.Join(" ", completion.Properties.Values);
        Assert.DoesNotContain(AuthorizationSentinel, serializedLog, StringComparison.Ordinal);
        Assert.DoesNotContain(BodySentinel, serializedLog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCompletion_InvalidCorrelationId_UsesTraceIdFallback()
    {
        var sink = new CollectingSink();
        await using var app = CreateApplication(sink);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/status/200");
        request.Headers.TryAddWithoutValidation(RequestCorrelationMiddleware.CorrelationHeaderName, "invalid correlation");

        using var response = await client.SendAsync(request);

        var returnedCorrelation = response.Headers
            .GetValues(RequestCorrelationMiddleware.CorrelationHeaderName)
            .Single();
        var completion = Assert.Single(sink.Events, IsRequestCompletion);
        Assert.NotEqual("invalid correlation", returnedCorrelation);
        Assert.Equal(completion.Properties["TraceId"].LiteralValue(), returnedCorrelation);
        Assert.Equal(completion.Properties["CorrelationId"].LiteralValue(), returnedCorrelation);
    }

    [Fact]
    public async Task RequestCompletion_HandledException_EmitsOneExceptionLogAndSeparateCompletion()
    {
        var sink = new CollectingSink();
        await using var app = CreateApplication(sink);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/exception")
        {
            Content = new StringContent(BodySentinel, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", AuthorizationSentinel);
        request.Headers.Add(RequestCorrelationMiddleware.CorrelationHeaderName, CorrelationId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CorrelationId, response.Headers.GetValues(RequestCorrelationMiddleware.CorrelationHeaderName).Single());
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("An unexpected error occurred.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal("/exception", problem.RootElement.GetProperty("instance").GetString());
        Assert.DoesNotContain("Sensitive internal detail", problem.RootElement.GetRawText(), StringComparison.Ordinal);

        var exceptionLog = Assert.Single(sink.Events, logEvent => logEvent.Exception is not null);
        Assert.Equal(LogEventLevel.Error, exceptionLog.Level);
        Assert.IsType<InvalidOperationException>(exceptionLog.Exception);
        Assert.False(string.IsNullOrWhiteSpace(exceptionLog.Exception.StackTrace));
        Assert.Equal(
            typeof(GlobalExceptionHandler).FullName,
            exceptionLog.Properties["SourceContext"].LiteralValue());
        Assert.Equal(StatusCodes.Status500InternalServerError, exceptionLog.Properties["StatusCode"].LiteralValue());
        Assert.Equal("POST", exceptionLog.Properties["RequestMethod"].LiteralValue());
        Assert.Equal("/exception", exceptionLog.Properties["RequestPath"].LiteralValue());
        Assert.Equal(CorrelationId, exceptionLog.Properties["CorrelationId"].LiteralValue());
        Assert.DoesNotContain(sink.Events, IsFrameworkUnhandledException);

        var completion = Assert.Single(sink.Events, IsRequestCompletion);
        Assert.NotSame(exceptionLog, completion);
        Assert.Null(completion.Exception);
        Assert.Equal(LogEventLevel.Error, completion.Level);
        Assert.Equal(StatusCodes.Status500InternalServerError, completion.Properties["StatusCode"].LiteralValue());
        Assert.True(Convert.ToDouble(completion.Properties["Elapsed"].LiteralValue()) >= 0);
        Assert.Equal("POST", completion.Properties["RequestMethod"].LiteralValue());
        Assert.Equal("/exception", completion.Properties["RequestPath"].LiteralValue());
        Assert.Equal(CorrelationId, completion.Properties["CorrelationId"].LiteralValue());
        Assert.False(string.IsNullOrWhiteSpace(completion.Properties["TraceId"].LiteralValue()?.ToString()));
        Assert.Equal(completion.Properties["TraceId"].LiteralValue(), exceptionLog.Properties["TraceId"].LiteralValue());
        var serializedLogs = string.Join(
            Environment.NewLine,
            sink.Events.Select(logEvent => logEvent.RenderMessage() + string.Join(" ", logEvent.Properties.Values)));
        Assert.DoesNotContain(AuthorizationSentinel, serializedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(BodySentinel, serializedLogs, StringComparison.Ordinal);
    }

    private static WebApplication CreateApplication(CollectingSink sink)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        RequestLoggingSinkRegistry.Current = sink;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:Using:0"] = typeof(RequestLoggingSinkConfigurationExtensions).Assembly.GetName().Name,
            ["Serilog:MinimumLevel:Default"] = "Verbose",
            ["Serilog:WriteTo:0:Name"] = "RequestLoggingTestSink"
        });
        builder.AddDefaultLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        var app = builder.Build();
        app.UseDefaultLogging();
        app.UseExceptionHandler();
        app.Run(async context =>
        {
            if (context.Request.Path == "/exception")
                throw new InvalidOperationException("Sensitive internal detail");

            _ = await new StreamReader(context.Request.Body).ReadToEndAsync(context.RequestAborted);
            context.Response.StatusCode = int.Parse(context.Request.Path.Value!.Split('/').Last());
        });
        return app;
    }

    private static bool IsRequestCompletion(LogEvent logEvent) =>
        logEvent.Properties.ContainsKey("StatusCode") &&
        logEvent.Properties.ContainsKey("Elapsed") &&
        logEvent.Properties.ContainsKey("CorrelationId") &&
        logEvent.Properties.ContainsKey("TraceId");

    private static bool IsFrameworkUnhandledException(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) &&
        Equals(sourceContext.LiteralValue(), "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware") &&
        logEvent.Properties.TryGetValue("EventId", out var eventId) &&
        eventId is StructureValue eventIdStructure &&
        eventIdStructure.Properties.Any(property =>
            property.Name == "Id" && Equals(property.Value.LiteralValue(), 1)) &&
        eventIdStructure.Properties.Any(property =>
            property.Name == "Name" && Equals(property.Value.LiteralValue(), "UnhandledException"));

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}

internal static class RequestLoggingSinkRegistry
{
    public static ILogEventSink? Current { get; set; }
}

public static class RequestLoggingSinkConfigurationExtensions
{
    public static LoggerConfiguration RequestLoggingTestSink(
        this LoggerSinkConfiguration sinkConfiguration) =>
        sinkConfiguration.Sink(RequestLoggingSinkRegistry.Current ??
            throw new InvalidOperationException("The request logging test sink was not configured."));
}

internal static class LogEventPropertyValueExtensions
{
    public static object? LiteralValue(this LogEventPropertyValue value) =>
        Assert.IsType<ScalarValue>(value).Value;
}
