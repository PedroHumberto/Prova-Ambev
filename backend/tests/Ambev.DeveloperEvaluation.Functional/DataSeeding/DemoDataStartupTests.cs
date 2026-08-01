using System.Net;
using Ambev.DeveloperEvaluation.Functional.Infrastructure;
using Ambev.DeveloperEvaluation.Functional.Support;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.DataSeeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.DataSeeding;

[Collection(FunctionalApiCollection.Name)]
public sealed class DemoDataStartupTests(FunctionalApiFixture fixture)
{
    [Fact]
    public async Task Startup_DemoDataDisabled_DoesNotSeedDemoMarkers()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        var demoUserExists = await context.Users.AsNoTracking()
            .AnyAsync(user => user.Email == DemoDataSeeder.DemoUserEmail);
        var demoSaleExists = await context.Sales.AsNoTracking()
            .AnyAsync(sale => sale.SaleNumber == DemoDataSeeder.DemoSaleNumber);

        Assert.False(demoUserExists);
        Assert.False(demoSaleExists);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DemoDataEnabledCollection : ICollectionFixture<DemoDataEnabledFixture>
{
    public const string Name = "Demo data enabled functional tests";
}

public sealed class DemoDataEnabledFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ambev_demo_data_functional")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private FunctionalWebApplicationFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The demo data API host has not been initialized.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new FunctionalWebApplicationFactory(_postgres.GetConnectionString(), demoDataEnabled: true);
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

[Collection(DemoDataEnabledCollection.Name)]
public sealed class DemoDataEnabledStartupTests(DemoDataEnabledFixture fixture)
{
    private readonly ApiTestClient _api = new(fixture.Client);

    [Fact]
    public async Task Startup_DemoDataEnabled_AuthenticatesAndReturnsSeededSaleWithoutOutboxMessages()
    {
        var (authenticationResponse, authenticationBody) = await _api.PostAsync("/api/Auth", new
        {
            email = DemoDataSeeder.DemoUserEmail,
            password = "DemoPassword1!"
        });

        Assert.Equal(HttpStatusCode.OK, authenticationResponse.StatusCode);
        Assert.True(authenticationBody.GetProperty("success").GetBoolean());
        var token = authenticationBody.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var (salesResponse, salesBody) = await _api.GetAsync(
            $"/api/sales?saleNumber={DemoDataSeeder.DemoSaleNumber}",
            token);

        Assert.Equal(HttpStatusCode.OK, salesResponse.StatusCode);
        var salesData = salesBody.GetProperty("data");
        Assert.Equal(1, salesData.GetProperty("totalCount").GetInt32());
        var sale = Assert.Single(salesData.GetProperty("items").EnumerateArray());
        Assert.Equal(DemoDataSeeder.DemoSaleNumber, sale.GetProperty("saleNumber").GetString());
        Assert.Equal("Active", sale.GetProperty("status").GetString());
        Assert.Equal(350.00m, sale.GetProperty("totalAmount").GetDecimal());

        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        Assert.Equal(0, await context.OutboxMessages.AsNoTracking().CountAsync());
    }
}
