using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Bogus;
using System.Globalization;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales.TestData;

internal static class SaleTestData
{
    internal static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid ProductOneId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly Guid ProductTwoId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    internal static readonly Guid ProductThreeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    internal static readonly DateTime SaleDate = new(2026, 7, 30, 14, 30, 0, DateTimeKind.Utc);
    internal static readonly DateTime CreatedAt = new(2026, 7, 30, 14, 31, 0, DateTimeKind.Utc);

    internal static Sale CreateSale(
        TestTimeProvider? timeProvider = null,
        params SaleItemInput[] items)
    {
        return Sale.Create(
            "SALE-2026-0001",
            SaleDate,
            CustomerId,
            "Customer One",
            BranchId,
            "Branch One",
            items.Length == 0 ? [Item()] : items,
            timeProvider ?? new TestTimeProvider(CreatedAt));
    }

    internal static SaleItemInput Item(
        Guid? productId = null,
        string productName = "Product One",
        int quantity = 1,
        decimal unitPrice = 10m)
    {
        return new SaleItemInput(productId ?? ProductOneId, productName, quantity, unitPrice);
    }

    internal static SaleItemReplacement Replacement(SaleItem item)
    {
        return new SaleItemReplacement(item.Id, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice);
    }
}

internal sealed class SaleBuilder
{
    private readonly Faker _faker = new("en")
    {
        Random = new Randomizer(20260730)
    };
    private SaleItemInput[]? _items;

    internal SaleBuilder WithItems(params SaleItemInput[] items)
    {
        _items = items;
        return this;
    }

    internal Sale Build()
    {
        var items = _items ??
        [
            new SaleItemInput(
                _faker.Random.Guid(),
                _faker.Commerce.ProductName(),
                _faker.Random.Int(1, 20),
                decimal.Parse(_faker.Commerce.Price(1, 100, 2), CultureInfo.InvariantCulture))
        ];

        return Sale.Create(
            $"SALE-{_faker.Random.AlphaNumeric(12).ToUpperInvariant()}",
            SaleTestData.SaleDate,
            _faker.Random.Guid(),
            _faker.Company.CompanyName(),
            _faker.Random.Guid(),
            _faker.Company.CompanyName(),
            items,
            new TestTimeProvider(SaleTestData.CreatedAt));
    }
}

internal sealed class TestTimeProvider(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = new(utcNow);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void SetUtcNow(DateTime value)
    {
        _utcNow = new DateTimeOffset(value);
    }
}
