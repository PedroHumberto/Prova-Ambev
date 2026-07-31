using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Outbox;

[Collection(PostgreSqlCollection.Name)]
public sealed class OutboxPersistenceTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CommitAsync_NewSale_PersistsSaleAndCreatedEventInSameCommit()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var sale = CreateSale("OUTBOX-COMMIT");
        await using (var context = database.CreateContext())
        {
            await new SaleRepository(context).AddAsync(sale, CancellationToken.None);

            await context.CommitAsync(CancellationToken.None);
        }

        await using var verificationContext = database.CreateContext();
        var message = await verificationContext.OutboxMessages.AsNoTracking().SingleAsync(
            CancellationToken.None);
        var integrationEvent = Assert.IsType<SaleCreatedV1>(
            SalesIntegrationEventMapper.Deserialize(message.Type, message.Payload));

        Assert.Equal(sale.Id, await verificationContext.Sales.Select(entity => entity.Id).SingleAsync(
            CancellationToken.None));
        Assert.Equal(message.Id, integrationEvent.EventId);
        Assert.Equal(sale.Id, integrationEvent.SaleId);
        Assert.Equal(SalesIntegrationEventMapper.SaleCreatedType, message.Type);
        Assert.Empty(sale.DomainEvents);
    }

    [Fact]
    public async Task CommitAsync_TransactionRolledBack_PersistsNeitherSaleNorOutbox()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var sale = CreateSale("OUTBOX-ROLLBACK");
        await using (var context = database.CreateContext())
        await using (var transaction = await context.Database.BeginTransactionAsync(CancellationToken.None))
        {
            await new SaleRepository(context).AddAsync(sale, CancellationToken.None);
            await context.CommitAsync(CancellationToken.None);
            Assert.Equal(1, await context.Sales.CountAsync(CancellationToken.None));
            Assert.Equal(1, await context.OutboxMessages.CountAsync(CancellationToken.None));

            await transaction.RollbackAsync(CancellationToken.None);
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(0, await verificationContext.Sales.CountAsync(CancellationToken.None));
        Assert.Equal(0, await verificationContext.OutboxMessages.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CommitAsync_TransientSaveFailureRetriedInSameContext_CapturesEventOnlyOnce()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var interceptor = new FailFirstSaveInterceptor();
        await using var context = database.CreateContext(interceptor);
        var sale = CreateSale("OUTBOX-RETRY");
        await new SaleRepository(context).AddAsync(sale, CancellationToken.None);

        await Assert.ThrowsAsync<ExpectedSaveFailure>(() =>
            context.CommitAsync(CancellationToken.None));
        await context.CommitAsync(CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);

        Assert.Equal(1, await context.Sales.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.OutboxMessages.CountAsync(CancellationToken.None));
        Assert.Empty(sale.DomainEvents);
    }

    [Fact]
    public async Task ClaimPendingAsync_RowLockedByAnotherDispatcher_SkipsLockedMessage()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var first = CreateMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Now);
        var second = CreateMessage(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Now.AddSeconds(1));
        await InsertMessagesAsync(database, first, second);

        await using var lockingContext = database.CreateContext();
        await using var transaction = await lockingContext.Database.BeginTransactionAsync(CancellationToken.None);
        await lockingContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"OutboxMessages\" WHERE \"Id\" = {first.Id} FOR UPDATE",
            CancellationToken.None);
        await using var claimingContext = database.CreateContext();

        var claimed = await new OutboxStore(claimingContext).ClaimPendingAsync(
            10,
            Now.AddMinutes(1),
            Now.AddMinutes(2),
            CancellationToken.None);

        Assert.Equal(second.Id, Assert.Single(claimed).Id);
        await transaction.RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ClaimPendingAsync_LeaseExpires_ReclaimsWithNewOwnership()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var message = CreateMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Now);
        await InsertMessagesAsync(database, message);
        ClaimedOutboxMessage firstClaim;
        await using (var firstContext = database.CreateContext())
        {
            firstClaim = Assert.Single(await new OutboxStore(firstContext).ClaimPendingAsync(
                1, Now, Now.AddSeconds(30), CancellationToken.None));
        }

        await using (var beforeExpiryContext = database.CreateContext())
        {
            Assert.Empty(await new OutboxStore(beforeExpiryContext).ClaimPendingAsync(
                1, Now.AddSeconds(29), Now.AddSeconds(59), CancellationToken.None));
        }

        await using var afterExpiryContext = database.CreateContext();
        var reclaimed = Assert.Single(await new OutboxStore(afterExpiryContext).ClaimPendingAsync(
            1, Now.AddSeconds(31), Now.AddSeconds(61), CancellationToken.None));

        Assert.Equal(firstClaim.Id, reclaimed.Id);
        Assert.NotEqual(firstClaim.LockId, reclaimed.LockId);
    }

    [Fact]
    public async Task MarkPublishedAsync_WrongOwnerRejectedThenOwnerMarksPublished()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var message = CreateMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Now);
        await InsertMessagesAsync(database, message);
        await using var context = database.CreateContext();
        var store = new OutboxStore(context);
        var claim = Assert.Single(await store.ClaimPendingAsync(
            1, Now, Now.AddMinutes(1), CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MarkPublishedAsync(
            claim.Id, Guid.NewGuid(), Now.AddSeconds(1), CancellationToken.None));
        await store.MarkPublishedAsync(
            claim.Id, claim.LockId, Now.AddSeconds(1), CancellationToken.None);

        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(Now.AddSeconds(1), persisted.PublishedAt);
        Assert.Null(persisted.LockId);
        Assert.Null(persisted.LockedUntil);
        Assert.Null(persisted.DeadLetteredAt);
    }

    [Fact]
    public async Task DispatchBatchAsync_PublisherSucceeds_PersistsPublishedState()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var message = CreateMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Now);
        await InsertMessagesAsync(database, message);
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));

        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(Now, persisted.PublishedAt);
        Assert.Null(persisted.DeadLetteredAt);
        Assert.Null(persisted.LockId);
        await publisher.Received(1).PublishAsync(
            Arg.Is<ISaleIntegrationEventV1>(integrationEvent => integrationEvent.EventId == message.Id),
            CancellationToken.None);
    }

    [Fact]
    public async Task DispatchBatchAsync_TransportFailureAfterManyAttempts_RemainsPendingAndSchedulesRetry()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var message = CreateMessage(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Now);
        await InsertMessagesAsync(database, message);
        await SetAttemptsAsync(database, message.Id, 42);
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        publisher.PublishAsync(Arg.Any<ISaleIntegrationEventV1>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("transport unavailable"));
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));

        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(43, persisted.Attempts);
        Assert.Equal(Now.AddMinutes(1), persisted.NextAttemptAt);
        Assert.Equal("transport unavailable", persisted.LastError);
        Assert.Null(persisted.PublishedAt);
        Assert.Null(persisted.DeadLetteredAt);
        Assert.Null(persisted.LockId);
    }

    [Fact]
    public async Task DispatchBatchAsync_UnsupportedPersistedType_DeadLettersDeterministically()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var message = new OutboxMessage(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "sales.unsupported.v1",
            "{}",
            Now,
            Now);
        await InsertMessagesAsync(database, message);
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));

        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(1, persisted.Attempts);
        Assert.Equal(Now, persisted.DeadLetteredAt);
        Assert.Contains("not supported", persisted.LastError, StringComparison.Ordinal);
        Assert.Null(persisted.PublishedAt);
        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<ISaleIntegrationEventV1>(), Arg.Any<CancellationToken>());
    }

    private static OutboxDispatcher CreateDispatcher(
        DefaultContext context,
        IIntegrationEventPublisher publisher) => new(
        new OutboxStore(context),
        publisher,
        Options.Create(new SalesMessagingOptions
        {
            OutboxBatchSize = 10,
            LeaseSeconds = 30,
            InitialRetryDelaySeconds = 5,
            MaximumRetryDelaySeconds = 60
        }),
        new FixedTimeProvider(Now),
        NullLogger<OutboxDispatcher>.Instance);

    private static async Task InsertMessagesAsync(TestDatabase database, params OutboxMessage[] messages)
    {
        await using var context = database.CreateContext();
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task SetAttemptsAsync(TestDatabase database, Guid id, int attempts)
    {
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"OutboxMessages\" SET \"Attempts\" = {attempts} WHERE \"Id\" = {id}",
            CancellationToken.None);
    }

    private static OutboxMessage CreateMessage(Guid id, DateTime createdAt)
    {
        var mapped = SalesIntegrationEventMapper.Map(
            new Ambev.DeveloperEvaluation.Domain.Sales.Events.SaleCreated(Guid.NewGuid(), Now),
            id,
            createdAt);
        return mapped;
    }

    private static Sale CreateSale(string number) => Sale.Create(
        number,
        Now,
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Customer snapshot",
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "Branch snapshot",
        [new SaleItemInput(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Product", 4, 10m)],
        new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class FailFirstSaveInterceptor : SaveChangesInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new ExpectedSaveFailure();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ExpectedSaveFailure : Exception;
}
