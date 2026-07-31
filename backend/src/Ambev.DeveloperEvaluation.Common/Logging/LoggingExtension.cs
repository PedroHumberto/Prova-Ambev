using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog.Templates;
using System.Diagnostics;

namespace Ambev.DeveloperEvaluation.Common.Logging;



/// <summary> Add default Logging configuration to project. This configuration supports Serilog logs with DataDog compatible output.</summary>
public static class LoggingExtension
{
    private const string ExceptionHandlerMiddlewareSourceContext =
        "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
    private const int UnhandledExceptionEventId = 1;
    private const string UnhandledExceptionEventName = "UnhandledException";

    /// <summary>
    /// The destructuring options builder configured with the default exception destructurers.
    /// </summary>
    static readonly DestructuringOptionsBuilder _destructuringOptionsBuilder = new DestructuringOptionsBuilder()
        .WithDefaultDestructurers();

    /// <summary>
    /// This method configures the logging with commonly used features for DataDog integration.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder" /> to add services to.</param>
    /// <returns>A <see cref="WebApplicationBuilder"/> that can be used to further configure the API services.</returns>
    /// <remarks>
    /// <para>Logging output are diferents on Debug and Release modes.</para>
    /// </remarks> 
    public static WebApplicationBuilder AddDefaultLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration().CreateLogger();
        builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails(_destructuringOptionsBuilder)
                .Filter.ByExcluding(IsDuplicateExceptionHandlerLog);

            if (Debugger.IsAttached)
            {
                loggerConfiguration.Enrich.WithProperty("DebuggerAttached", Debugger.IsAttached);
                loggerConfiguration.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}", theme: SystemConsoleTheme.Colored);
            }
            else
            {
                loggerConfiguration
                    .WriteTo.Console
                    (
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"
                    )
                    .WriteTo.File(
                        "logs/log-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"
                    );
            }
        });

        builder.Services.AddLogging();

        return builder;
    }

    /// <summary>Adds middleware for Swagger documetation generation.</summary>
    /// <param name="app">The <see cref="WebApplication"/> instance this method extends.</param>
    /// <returns>The <see cref="WebApplication"/> for Swagger documentation.</returns>
    public static WebApplication UseDefaultLogging(this WebApplication app)
    {
        app.UseMiddleware<RequestCorrelationMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms (CorrelationId: {CorrelationId}, TraceId: {TraceId})";
            options.GetLevel = (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                    return LogEventLevel.Error;

                return httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", RequestCorrelationMiddleware.GetCorrelationId(httpContext));
                diagnosticContext.Set("TraceId", RequestCorrelationMiddleware.GetTraceId(httpContext));
            };
        });

        var logger = app.Services.GetRequiredService<ILogger<Logger>>();

        var mode = Debugger.IsAttached ? "Debug" : "Release";
        logger.LogInformation("Logging enabled for '{Application}' on '{Environment}' - Mode: {Mode}", app.Environment.ApplicationName, app.Environment.EnvironmentName, mode);
        return app;

    }

    private static bool IsDuplicateExceptionHandlerLog(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) ||
            sourceContext is not ScalarValue { Value: ExceptionHandlerMiddlewareSourceContext })
        {
            return false;
        }

        if (!logEvent.Properties.TryGetValue("EventId", out var eventId) ||
            eventId is not StructureValue eventIdStructure)
        {
            return false;
        }

        var idMatches = eventIdStructure.Properties.Any(property =>
            property.Name == "Id" &&
            property.Value is ScalarValue { Value: UnhandledExceptionEventId });
        var nameMatches = eventIdStructure.Properties.Any(property =>
            property.Name == "Name" &&
            property.Value is ScalarValue { Value: UnhandledExceptionEventName });

        return idMatches && nameMatches;
    }
}
