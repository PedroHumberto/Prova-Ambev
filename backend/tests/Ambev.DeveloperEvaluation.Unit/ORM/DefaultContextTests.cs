using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Mapping;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.ORM;

public class DefaultContextTests
{
    [Fact]
    public async Task CommitAsync_DuplicateUserEmailConstraint_TranslatesException()
    {
        var postgresException = CreateUniqueViolation(UserConfiguration.UserEmailUniqueIndexName);
        var updateException = new DbUpdateException("Update failed.", postgresException);
        await using var context = new FailingDefaultContext(updateException);

        var action = () => context.CommitAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DuplicateUserEmailException>();
        exception.Which.InnerException.Should().BeSameAs(updateException);
    }

    [Fact]
    public async Task CommitAsync_DifferentUniqueConstraint_DoesNotTranslateException()
    {
        var postgresException = CreateUniqueViolation("PK_Users");
        var updateException = new DbUpdateException("Update failed.", postgresException);
        await using var context = new FailingDefaultContext(updateException);

        var action = () => context.CommitAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.Should().BeSameAs(updateException);
    }

    [Fact]
    public async Task CommitAsync_DuplicateSaleNumberConstraint_TranslatesException()
    {
        var postgresException = CreateUniqueViolation(SaleConfiguration.SaleNumberUniqueIndexName);
        var updateException = new DbUpdateException("Update failed.", postgresException);
        await using var context = new FailingDefaultContext(updateException);

        var action = () => context.CommitAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DuplicateSaleNumberException>();
        exception.Which.InnerException.Should().BeSameAs(updateException);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_NullOperation_ThrowsArgumentNullException()
    {
        await using var context = new DefaultContext(new DbContextOptionsBuilder<DefaultContext>().Options);

        var action = () => context.ExecuteInTransactionAsync<int>(null!);

        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("operation");
    }

    [Fact]
    public async Task SaveChangesAsync_FailedPersistence_CapturesDomainEventOnceAndPreservesItForRetry()
    {
        await using var context = new DefaultContext(new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql("Host=localhost;Database=cancelled_save;Username=test;Password=test")
            .Options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var occurredAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var sale = Sale.Create(
            "SALE-OUTBOX-1",
            occurredAt,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Customer",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Branch",
            [new SaleItemInput(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Product", 1, 10m)],
            new FixedTimeProvider(occurredAt));
        context.Sales.Add(sale);

        var firstAttempt = () => context.SaveChangesAsync(cancellation.Token);
        await firstAttempt.Should().ThrowAsync<OperationCanceledException>();
        var secondAttempt = () => context.SaveChangesAsync(cancellation.Token);
        await secondAttempt.Should().ThrowAsync<OperationCanceledException>();

        context.ChangeTracker.Entries<OutboxMessage>().Should().ContainSingle();
        sale.DomainEvents.Should().ContainSingle();
    }

    private static PostgresException CreateUniqueViolation(string constraintName) => new(
        "duplicate key value violates unique constraint",
        "ERROR",
        "ERROR",
        PostgresErrorCodes.UniqueViolation,
        constraintName: constraintName);

    private sealed class FailingDefaultContext(DbUpdateException exception)
        : DefaultContext(new DbContextOptionsBuilder<DefaultContext>().Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(exception);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
