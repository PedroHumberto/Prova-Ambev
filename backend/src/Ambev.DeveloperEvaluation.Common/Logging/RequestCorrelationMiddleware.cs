using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Ambev.DeveloperEvaluation.Common.Logging;

public sealed class RequestCorrelationMiddleware
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

    private const int MaximumCorrelationIdLength = 128;
    private static readonly object CorrelationIdKey = new();
    private static readonly object TraceIdKey = new();
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var traceId = Activity.Current?.TraceId.ToString();
        if (string.IsNullOrWhiteSpace(traceId))
            traceId = context.TraceIdentifier;

        var correlationId = TryGetCorrelationId(context.Request.Headers, out var suppliedCorrelationId)
            ? suppliedCorrelationId
            : traceId;

        context.Items[CorrelationIdKey] = correlationId;
        context.Items[TraceIdKey] = traceId;
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            httpContext.Response.Headers[CorrelationHeaderName] = GetCorrelationId(httpContext);
            return Task.CompletedTask;
        }, context);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId
        }))
        {
            await _next(context);
        }
    }

    internal static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    internal static string GetTraceId(HttpContext context) =>
        context.Items.TryGetValue(TraceIdKey, out var value) && value is string traceId
            ? traceId
            : context.TraceIdentifier;

    private static bool TryGetCorrelationId(
        IHeaderDictionary headers,
        out string correlationId)
    {
        correlationId = string.Empty;
        if (!headers.TryGetValue(CorrelationHeaderName, out StringValues values) || values.Count != 1)
            return false;

        var candidate = values[0];
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaximumCorrelationIdLength)
            return false;

        foreach (var character in candidate)
        {
            var isAsciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';
            if (!isAsciiLetter && !isDigit && character is not '-' and not '_' and not '.')
                return false;
        }

        correlationId = candidate;
        return true;
    }
}
