using System.Diagnostics;
using Ambev.DeveloperEvaluation.Common.Logging;
using Ambev.DeveloperEvaluation.Unit.TestInfrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Common;

public sealed class RequestCorrelationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidCorrelationId_EchoesHeaderWhenResponseStarts()
    {
        const string correlationId = "client.request_123";
        var context = CreateContext(correlationId);
        var middleware = new RequestCorrelationMiddleware(
            next: StartResponseAsync,
            new RecordingLogger<RequestCorrelationMiddleware>());

        await middleware.InvokeAsync(context);

        context.Response.Headers[RequestCorrelationMiddleware.CorrelationHeaderName]
            .Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("não-ascii")]
    public async Task InvokeAsync_InvalidCorrelationId_UsesCurrentTraceId(string invalidCorrelationId)
    {
        using var activity = new Activity("correlation-test").Start();
        var context = CreateContext(invalidCorrelationId);
        var middleware = new RequestCorrelationMiddleware(
            next: StartResponseAsync,
            new RecordingLogger<RequestCorrelationMiddleware>());

        await middleware.InvokeAsync(context);

        context.Response.Headers[RequestCorrelationMiddleware.CorrelationHeaderName]
            .Should().ContainSingle().Which.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_OverlongCorrelationId_UsesCurrentTraceId()
    {
        using var activity = new Activity("overlong-correlation-test").Start();
        var context = CreateContext(new string('a', 129));
        var middleware = new RequestCorrelationMiddleware(
            next: StartResponseAsync,
            new RecordingLogger<RequestCorrelationMiddleware>());

        await middleware.InvokeAsync(context);

        context.Response.Headers[RequestCorrelationMiddleware.CorrelationHeaderName]
            .Should().ContainSingle().Which.Should().Be(activity.TraceId.ToString());
    }

    private static DefaultHttpContext CreateContext(string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartingResponseFeature());
        context.Request.Headers[RequestCorrelationMiddleware.CorrelationHeaderName] = correlationId;
        return context;
    }

    private static Task StartResponseAsync(HttpContext context) =>
        context.Features.GetRequiredFeature<IHttpResponseFeature>() is StartingResponseFeature feature
            ? feature.StartAsync()
            : throw new InvalidOperationException("The response feature was not configured.");

    private sealed class StartingResponseFeature : IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _callbacks = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => _callbacks.Push((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task StartAsync()
        {
            while (_callbacks.TryPop(out var callback))
                await callback.Callback(callback.State);

            HasStarted = true;
        }
    }
}
