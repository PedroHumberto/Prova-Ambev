using Ambev.DeveloperEvaluation.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FunctionalApiCollection : ICollectionFixture<FunctionalApiFixture>
{
    public const string Name = "Functional API tests";
}

public sealed class FunctionalApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ambev_functional")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private FunctionalWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new FunctionalWebApplicationFactory(_postgres.GetConnectionString());
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

internal sealed class FunctionalWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SecretKey"] = "YourSuperSecretKeyForJwtTokenGenerationThatShouldBeAtLeast32BytesLong",
                ["SalesMessaging:ConnectionString"] = "amqp://guest:guest@localhost:5672",
                ["SalesMessaging:OutboxBatchSize"] = "20",
                ["SalesMessaging:PollingIntervalMilliseconds"] = "1000",
                ["SalesMessaging:LeaseSeconds"] = "60",
                ["SalesMessaging:InitialRetryDelaySeconds"] = "5",
                ["SalesMessaging:MaximumRetryDelaySeconds"] = "300"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            // RabbitMQ publishing and outbox dispatch are real integration-test concerns. The HTTP
            // suite keeps real outbox persistence while removing background services from its host.
            services.RemoveAll<IHostedService>();
        });
    }
}
