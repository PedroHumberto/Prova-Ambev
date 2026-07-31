using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Common.HealthChecks;
using Ambev.DeveloperEvaluation.Common.Logging;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Common.Validation;
using Ambev.DeveloperEvaluation.IoC;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using Ambev.DeveloperEvaluation.WebApi.Swagger;
using System.Net.Sockets;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;

namespace Ambev.DeveloperEvaluation.WebApi;

public class Program
{
    private static readonly TimeSpan[] DatabaseMigrationRetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(50),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(90)
    ];

    public static async Task Main(string[] args)
    {
        try
        {
            Log.Information("Starting web application");

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.AddDefaultLogging();

            builder.Services.AddControllers();
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddValidatorsFromAssembly(typeof(ApplicationLayer).Assembly);
            builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddEndpointsApiExplorer();

            builder.AddBasicHealthChecks();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter a JWT Bearer token.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                options.OperationFilter<AuthorizeOperationFilter>();
            });

            builder.Services.AddDbContext<DefaultContext>(options =>
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    options => options.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName)
                )
            );

            builder.Services.AddJwtAuthentication(builder.Configuration);

            builder.RegisterDependencies();

            builder.Services.AddAutoMapper(
                _ => { },
                typeof(Program).Assembly,
                typeof(ApplicationLayer).Assembly);

            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(
                    typeof(ApplicationLayer).Assembly,
                    typeof(Program).Assembly
                );
            });

            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            var app = builder.Build();
            app.UseDefaultLogging();
            app.UseExceptionHandler();
            app.UseStatusCodePages(async statusCodeContext =>
            {
                var httpContext = statusCodeContext.HttpContext;
                var (title, detail) = httpContext.Response.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => ("Bad Request", "The request could not be processed."),
                    StatusCodes.Status401Unauthorized => ("Unauthorized", "Authentication is required."),
                    StatusCodes.Status404NotFound => ("Not Found", "The requested resource was not found."),
                    _ => (null, null)
                };

                if (title is null)
                    return;

                var problemDetailsService = httpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>();

                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = httpContext.Response.StatusCode,
                        Title = title,
                        Detail = detail,
                        Instance = httpContext.Request.Path
                    }
                });
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseBasicHealthChecks();

            if (builder.Configuration.GetValue("HttpsRedirection:Enabled", true))
                app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await MigrateDatabaseWithRetryAsync(
                app.Services.GetRequiredService<IServiceScopeFactory>(),
                app.Logger,
                app.Lifetime.ApplicationStopping);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task MigrateDatabaseWithRetryAsync(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
                await context.Database.MigrateAsync(cancellationToken);

                if (attempt > 0)
                    logger.LogInformation("Database migrations completed after {Attempts} attempt(s)", attempt + 1);

                return;
            }
            catch (Exception exception) when (IsTransientDatabaseFailure(exception) && attempt < DatabaseMigrationRetryDelays.Length)
            {
                var delay = DatabaseMigrationRetryDelays[attempt];
                logger.LogWarning(
                    exception,
                    "Database is not ready. Retrying migrations in {RetryDelaySeconds} seconds (attempt {NextAttempt} of {TotalAttempts})",
                    delay.TotalSeconds,
                    attempt + 2,
                    DatabaseMigrationRetryDelays.Length + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransientDatabaseFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsqlException && npgsqlException.IsTransient ||
                current is SocketException or TimeoutException)
                return true;
        }

        return false;
    }
}
