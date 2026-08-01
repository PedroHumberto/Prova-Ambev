using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.ORM;

public sealed class RepositoryTrackingAndGuardTests
{
    private static readonly DateTime SaleDate = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaleRepository_AddAsync_ValidSale_TracksAggregateWithoutDatabaseAccess()
    {
        await using var context = CreateContext();
        var repository = new SaleRepository(context);
        var sale = CreateSale();

        await repository.AddAsync(sale, CancellationToken.None);

        context.Entry(sale).State.Should().Be(EntityState.Added);
        context.Entry(sale.Items.Single()).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task SaleRepository_AddAsync_NullSale_ThrowsArgumentNullException()
    {
        await using var context = CreateContext();
        var repository = new SaleRepository(context);

        var action = () => repository.AddAsync(null!, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("sale");
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task SaleRepository_GetByIdForUpdateAsync_WithoutTransaction_RejectsLockAttempt()
    {
        await using var context = CreateContext();
        var repository = new SaleRepository(context);

        var action = () => repository.GetByIdForUpdateAsync(Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A transaction must be active before locking a sale for update.");
    }

    [Theory]
    [InlineData(0, 10, "criteria.PageNumber")]
    [InlineData(1, 0, "criteria.PageSize")]
    public async Task SaleRepository_GetPageAsync_InvalidPaging_ThrowsBeforeDatabaseAccess(
        int pageNumber,
        int pageSize,
        string parameterName)
    {
        await using var context = CreateContext();
        var repository = new SaleRepository(context);
        var criteria = new SaleQueryCriteria { PageNumber = pageNumber, PageSize = pageSize };

        var action = () => repository.GetPageAsync(criteria, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [Fact]
    public async Task UserRepository_CreateAsync_ValidUser_TracksAndReturnsSameInstance()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = new User { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Email = "user@example.com" };

        var result = await repository.CreateAsync(user, CancellationToken.None);

        result.Should().BeSameAs(user);
        context.Entry(user).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void OutboxStore_NullContext_ThrowsArgumentNullException()
    {
        var action = () => new OutboxStore(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task OutboxStore_ClaimPendingAsync_NonPositiveBatchSize_ThrowsBeforeDatabaseAccess()
    {
        await using var context = CreateContext();
        var store = new OutboxStore(context);

        var action = () => store.ClaimPendingAsync(0, SaleDate, SaleDate.AddMinutes(1), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("batchSize");
    }

    [Fact]
    public async Task OutboxStore_MarkFailedAsync_WhitespaceError_ThrowsBeforeDatabaseAccess()
    {
        await using var context = CreateContext();
        var store = new OutboxStore(context);

        var action = () => store.MarkFailedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SaleDate,
            SaleDate.AddMinutes(1),
            " ",
            false,
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithParameterName("error");
    }

    private static DefaultContext CreateContext() => new(new DbContextOptionsBuilder<DefaultContext>()
        .UseNpgsql("Host=localhost;Database=tracking_only;Username=test;Password=test")
        .Options);

    private static Sale CreateSale() => Sale.Create(
        "SALE-ORM-1",
        SaleDate,
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "Customer",
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        "Branch",
        [new SaleItemInput(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Product", 4, 10m)],
        new FixedTimeProvider(SaleDate));

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
