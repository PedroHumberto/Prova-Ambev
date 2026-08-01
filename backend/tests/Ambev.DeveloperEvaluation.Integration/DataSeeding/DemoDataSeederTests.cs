using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.DataSeeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.DataSeeding;

[Collection(PostgreSqlCollection.Name)]
public sealed class DemoDataSeederTests(PostgreSqlFixture fixture)
{
    private const string DemoPassword = "DemoPassword1!";

    private readonly IPasswordHasher _passwordHasher = new BCryptPasswordHasher();

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesValidDemoUserAndSalesWithoutOutboxMessages()
    {
        await using var database = await fixture.CreateDatabaseAsync();

        await SeedAsync(database);

        await using var context = database.CreateContext();
        var user = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(DemoDataSeeder.DemoUserEmail, user.Email);
        Assert.Equal("Demo Administrator", user.Username);
        Assert.Equal("+5511999999999", user.Phone);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(_passwordHasher.VerifyPassword(DemoPassword, user.Password));

        Assert.Equal(6, await context.Sales.CountAsync());
        var sale = await context.Sales
            .AsNoTracking()
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.SaleNumber == DemoDataSeeder.DemoSaleNumber);
        Assert.Equal(DemoDataSeeder.DemoSaleNumber, sale.SaleNumber);
        Assert.Equal(new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc), sale.SaleDate);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), sale.CustomerId);
        Assert.Equal("Demo Customer", sale.CustomerName);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), sale.BranchId);
        Assert.Equal("Demo Branch", sale.BranchName);
        Assert.Equal(SaleStatus.Active, sale.Status);
        Assert.Null(sale.CancelledAt);
        Assert.Equal(420.00m, sale.Subtotal);
        Assert.Equal(70.00m, sale.DiscountAmount);
        Assert.Equal(350.00m, sale.TotalAmount);

        var items = sale.Items.OrderBy(item => item.Quantity).ToArray();
        Assert.Collection(
            items,
            item => AssertItem(item, "33333333-3333-3333-3333-333333333333", 2, 10.00m, 0, 20.00m, 0.00m, 20.00m),
            item => AssertItem(item, "44444444-4444-4444-4444-444444444444", 5, 20.00m, 10, 100.00m, 10.00m, 90.00m),
            item => AssertItem(item, "55555555-5555-5555-5555-555555555555", 10, 30.00m, 20, 300.00m, 60.00m, 240.00m));
        Assert.Empty(await context.OutboxMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_ExecutedTwice_DoesNotDuplicateMarkersOrCreateOutboxMessages()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await SeedAsync(database);
        await SeedAsync(database);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync(user => user.Email == DemoDataSeeder.DemoUserEmail));
        Assert.Equal(6, await context.Sales.CountAsync());
        Assert.Equal(1, await context.Sales.CountAsync(sale => sale.SaleNumber == DemoDataSeeder.DemoSaleNumber));
        Assert.Equal(3, await context.Sales
            .Where(sale => sale.SaleNumber == DemoDataSeeder.DemoSaleNumber)
            .SelectMany(sale => sale.Items)
            .CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_CompatibleUserAlreadyExists_PreservesUserAndCreatesSales()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var existingUser = CreateDemoUser();
        await using (var arrangeContext = database.CreateContext())
        {
            await arrangeContext.Users.AddAsync(existingUser);
            await arrangeContext.CommitAsync();
        }

        await SeedAsync(database);

        await using var context = database.CreateContext();
        var persistedUser = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(existingUser.Id, persistedUser.Id);
        Assert.Equal(existingUser.Username, persistedUser.Username);
        Assert.Equal(existingUser.Password, persistedUser.Password);
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(6, await context.Sales.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_CancelledDemoSaleAlreadyExists_PreservesSaleAndCreatesRemainingData()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var existingSale = CreateExistingDemoSale();
        existingSale.Cancel();
        existingSale.ClearDomainEvents();
        await using (var arrangeContext = database.CreateContext())
        {
            await arrangeContext.Sales.AddAsync(existingSale);
            await arrangeContext.CommitAsync();
        }

        await SeedAsync(database);

        await using var context = database.CreateContext();
        var persistedSale = await context.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .SingleAsync(sale => sale.SaleNumber == DemoDataSeeder.DemoSaleNumber);
        Assert.Equal(existingSale.Id, persistedSale.Id);
        Assert.Equal(SaleStatus.Cancelled, persistedSale.Status);
        Assert.NotNull(persistedSale.CancelledAt);
        Assert.Equal("Changed Customer", persistedSale.CustomerName);
        Assert.Single(persistedSale.Items);
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(6, await context.Sales.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Theory]
    [InlineData(DemoUserIncompatibility.Status)]
    [InlineData(DemoUserIncompatibility.Role)]
    [InlineData(DemoUserIncompatibility.Password)]
    public async Task SeedAsync_IncompatibleDemoUserExists_ThrowsAndDoesNotOverwriteOrCreateSale(
        DemoUserIncompatibility incompatibility)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var incompatibleUser = CreateDemoUser();
        ApplyIncompatibility(incompatibleUser, incompatibility);
        await using (var arrangeContext = database.CreateContext())
        {
            await arrangeContext.Users.AddAsync(incompatibleUser);
            await arrangeContext.CommitAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(database));

        Assert.Contains(DemoDataSeeder.DemoUserEmail, exception.Message, StringComparison.Ordinal);
        await using var context = database.CreateContext();
        var persistedUser = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(incompatibleUser.Id, persistedUser.Id);
        Assert.Equal(incompatibleUser.Status, persistedUser.Status);
        Assert.Equal(incompatibleUser.Role, persistedUser.Role);
        Assert.Equal(incompatibleUser.Password, persistedUser.Password);
        Assert.Equal(0, await context.Sales.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_PreCancelledToken_CreatesNoData()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SeedAsync(database, cancellation.Token));

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.Sales.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_SaveCompletesThenFails_RollsBackAllDemoData()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using (var failingContext = database.CreateContext(new FailAfterSaveInterceptor()))
        {
            var seeder = new DemoDataSeeder(
                failingContext,
                _passwordHasher,
                NullLogger<DemoDataSeeder>.Instance);

            await Assert.ThrowsAsync<ExpectedSaveFailure>(() => seeder.SeedAsync());
        }

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.Sales.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    private async Task SeedAsync(TestDatabase database, CancellationToken cancellationToken = default)
    {
        await using var context = database.CreateContext();
        var seeder = new DemoDataSeeder(
            context,
            _passwordHasher,
            NullLogger<DemoDataSeeder>.Instance);
        await seeder.SeedAsync(cancellationToken);
    }

    private User CreateDemoUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "Existing Demo Administrator",
        Email = DemoDataSeeder.DemoUserEmail,
        Phone = "+5511888888888",
        Password = _passwordHasher.HashPassword(DemoPassword),
        Role = UserRole.Admin,
        Status = UserStatus.Active,
        CreatedAt = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc)
    };

    private void ApplyIncompatibility(User user, DemoUserIncompatibility incompatibility)
    {
        switch (incompatibility)
        {
            case DemoUserIncompatibility.Status:
                user.Status = UserStatus.Suspended;
                break;
            case DemoUserIncompatibility.Role:
                user.Role = UserRole.Manager;
                break;
            case DemoUserIncompatibility.Password:
                user.Password = _passwordHasher.HashPassword("DifferentPassword1!");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(incompatibility), incompatibility, null);
        }
    }

    private static Sale CreateExistingDemoSale()
    {
        return Sale.Create(
            DemoDataSeeder.DemoSaleNumber,
            new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Changed Customer",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Changed Branch",
            [new SaleItemInput(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Changed Product", 1, 1.00m)]);
    }

    private static void AssertItem(
        SaleItem item,
        string productId,
        int quantity,
        decimal unitPrice,
        int discountPercentage,
        decimal subtotal,
        decimal discountAmount,
        decimal totalAmount)
    {
        Assert.Equal(Guid.Parse(productId), item.ProductId);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(unitPrice, item.UnitPrice);
        Assert.Equal(discountPercentage, item.DiscountPercentage);
        Assert.Equal(subtotal, item.Subtotal);
        Assert.Equal(discountAmount, item.DiscountAmount);
        Assert.Equal(totalAmount, item.TotalAmount);
        Assert.Equal(SaleStatus.Active, item.Status);
        Assert.Null(item.CancelledAt);
    }

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            throw new ExpectedSaveFailure();
        }
    }

    private sealed class ExpectedSaveFailure : Exception;

    public enum DemoUserIncompatibility
    {
        Status,
        Role,
        Password
    }
}
