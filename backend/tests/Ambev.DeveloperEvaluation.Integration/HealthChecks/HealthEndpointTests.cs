using System.Net;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Common.HealthChecks;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.IoC.HealthChecks;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.IoC.ModuleInitializers;
using Ambev.DeveloperEvaluation.ORM;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.HealthChecks;

[Collection(PostgreSqlCollection.Name)]
public sealed class HealthEndpointTests(PostgreSqlFixture postgreSql)
{
    private const string SensitiveDescription = "Host=secret-host;Password=secret-password";

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task HealthEndpoints_RequiredDependencyUnavailable_LiveStaysHealthyAndReadyFails(
        bool postgreSqlHealthy,
        bool rabbitMqHealthy)
    {
        await using var app = CreateApplication(builder =>
        {
            builder.Services.AddHealthChecks()
                .AddCheck(
                    "postgresql",
                    () => Result(postgreSqlHealthy),
                    tags: ["readiness"])
                .AddCheck(
                    "rabbitmq",
                    () => Result(rabbitMqHealthy),
                    tags: ["readiness"]);
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        await AssertSanitizedAsync(ready, "postgresql", "rabbitmq");
        await AssertSanitizedAsync(health, "Liveness", "postgresql", "rabbitmq");
    }

    [Fact]
    public async Task HealthEndpoints_RealPostgreSqlAndRabbitMqAvailable_AllChecksAreHealthy()
    {
        await using var database = await postgreSql.CreateDatabaseAsync();
        await using var rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await rabbitMq.StartAsync();
        await using var app = CreateApplication(builder =>
        {
            builder.Services.AddDbContext<DefaultContext>(options => options.UseNpgsql(database.ConnectionString));
            builder.Services.AddSingleton<IOptions<SalesMessagingOptions>>(
                Options.Create(new SalesMessagingOptions
                {
                    ConnectionString = rabbitMq.GetConnectionString()
                }));
            builder.Services.AddHealthChecks()
                .AddDbContextCheck<DefaultContext>("postgresql", tags: ["readiness"])
                .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["readiness"], timeout: TimeSpan.FromSeconds(5));
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        using var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        await AssertSanitizedAsync(ready, "postgresql", "rabbitmq");
    }

    [Fact]
    public async Task RabbitMqHealthCheck_CancelledToken_PropagatesCancellation()
    {
        var options = Options.Create(new SalesMessagingOptions
        {
            ConnectionString = "amqp://guest:secret@192.0.2.1:5672/"
        });
        var healthCheck = new RabbitMqHealthCheck(options);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task RabbitMqHealthCheck_UnavailableBroker_ReturnsGenericFailureWithoutConnectionString()
    {
        const string connectionString = "amqp://guest:secret-password@127.0.0.1:1/";
        var healthCheck = new RabbitMqHealthCheck(Options.Create(new SalesMessagingOptions
        {
            ConnectionString = connectionString
        }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("RabbitMQ is unavailable.", result.Description);
        Assert.DoesNotContain(connectionString, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", result.ToString(), StringComparison.Ordinal);
    }

    private static WebApplication CreateApplication(Action<WebApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.AddBasicHealthChecks();
        configure(builder);
        var app = builder.Build();
        app.UseBasicHealthChecks();
        return app;
    }

    private static HealthCheckResult Result(bool healthy) => healthy
        ? HealthCheckResult.Healthy(SensitiveDescription)
        : HealthCheckResult.Unhealthy(SensitiveDescription, new InvalidOperationException(SensitiveDescription));

    private static async Task AssertSanitizedAsync(HttpResponseMessage response, params string[] expectedChecks)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SensitiveDescription, body, StringComparison.Ordinal);
        Assert.DoesNotContain("description", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(body);
        var checks = document.RootElement.GetProperty("healthChecks")
            .EnumerateArray()
            .Select(element => element.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(expectedChecks.Order(), checks.Order());
        Assert.All(document.RootElement.GetProperty("healthChecks").EnumerateArray(), check =>
        {
            Assert.True(check.TryGetProperty("name", out _));
            Assert.True(check.TryGetProperty("status", out _));
            Assert.True(check.TryGetProperty("hostEnvironment", out _));
            Assert.Equal(3, check.EnumerateObject().Count());
        });
    }
}

public sealed class HealthCheckRegistrationTests
{
    [Fact]
    public void Initialize_RequiredDependencies_RegistersReadinessChecksWithBoundedRabbitMqTimeout()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration[$"{SalesMessagingOptions.SectionName}:ConnectionString"] =
            "amqp://guest:guest@localhost:5672/";
        new InfrastructureModuleInitializer().Initialize(builder);
        using var provider = builder.Services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        var postgreSql = Assert.Single(registrations, registration => registration.Name == "postgresql");
        var rabbitMq = Assert.Single(registrations, registration => registration.Name == "rabbitmq");
        Assert.Contains("readiness", postgreSql.Tags);
        Assert.Contains("readiness", rabbitMq.Tags);
        Assert.Equal(TimeSpan.FromSeconds(5), rabbitMq.Timeout);
        Assert.Equal(HealthStatus.Unhealthy, rabbitMq.FailureStatus);
    }
}
