using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

[Collection(PostgreSqlCollection.Name)]
public sealed class CatalogRepositoryTests(PostgreSqlFixture fixture)
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SearchAsync_EmptySearch_ReturnsDistinctSnapshotsOrderedByNameAndLimited()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await SeedSnapshotsAsync(database);
        await using var context = database.CreateContext();
        var repository = new CatalogRepository(context);

        var customers = await repository.SearchCustomersAsync(null, 2);
        var branches = await repository.SearchBranchesAsync(null, 10);
        var products = await repository.SearchProductsAsync(null, 10);

        Assert.Collection(
            customers,
            item => Assert.Equal(new(CustomerId, "Acme North"), (item.Id, item.Name)),
            item => Assert.Equal(new(CustomerId, "Acme South"), (item.Id, item.Name)));
        Assert.Equal(["Central Branch", "West Branch"], branches.Select(item => item.Name));
        Assert.Equal(["Classic Lager", "Premium Malt"], products.Select(item => item.Name));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SearchAsync_MixedCaseContainsAndLiteralWildcards_ReturnsMatchingSnapshots()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await SeedSnapshotsAsync(database);
        await using var context = database.CreateContext();
        var repository = new CatalogRepository(context);

        var customers = await repository.SearchCustomersAsync("ME n", 10);
        var branches = await repository.SearchBranchesAsync("TRAL", 10);
        var products = await repository.SearchProductsAsync("%_", 10);

        Assert.Equal("Acme North", Assert.Single(customers).Name);
        Assert.Equal("Central Branch", Assert.Single(branches).Name);
        Assert.Empty(products);
    }

    private static async Task SeedSnapshotsAsync(TestDatabase database)
    {
        await using var context = database.CreateContext();
        var repository = new SaleRepository(context);
        await repository.AddAsync(CreateSale("CATALOG-1", "Acme South", "West Branch", "Premium Malt"));
        await repository.AddAsync(CreateSale("CATALOG-2", "Acme South", "West Branch", "Premium Malt"));
        await repository.AddAsync(CreateSale("CATALOG-3", "Acme North", "Central Branch", "Classic Lager"));
        await context.CommitAsync();
    }

    private static Sale CreateSale(
        string saleNumber,
        string customerName,
        string branchName,
        string productName) =>
        Sale.Create(
            saleNumber,
            new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            CustomerId,
            customerName,
            BranchId,
            branchName,
            [new SaleItemInput(ProductId, productName, 1, 10m)]);
}
