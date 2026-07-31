using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public sealed class OutboxDispatcherTests
{
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SaleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid LockId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime Now = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchBatchAsync_ValidMessagePublished_MarksClaimAsPublished()
    {
        var store = Substitute.For<IOutboxStore>();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var cancellationToken = new CancellationTokenSource().Token;
        store.ClaimPendingAsync(10, Now, Now.AddSeconds(30), cancellationToken)
            .Returns([CreateClaimedMessage()]);
        var dispatcher = CreateDispatcher(store, publisher);

        var processed = await dispatcher.DispatchBatchAsync(cancellationToken);

        Assert.Equal(1, processed);
        await publisher.Received(1).PublishAsync(
            Arg.Is<ISaleIntegrationEventV1>(message => message.EventId == EventId && message.SaleId == SaleId),
            cancellationToken);
        await store.Received(1).MarkPublishedAsync(EventId, LockId, Now, cancellationToken);
        await store.DidNotReceive().MarkFailedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_TransportFailsBeyondPreviousLimit_KeepsPendingWithCappedRetry()
    {
        var store = Substitute.For<IOutboxStore>();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var cancellationToken = new CancellationTokenSource().Token;
        store.ClaimPendingAsync(10, Now, Now.AddSeconds(30), cancellationToken)
            .Returns([CreateClaimedMessage(attempts: 42)]);
        publisher.PublishAsync(Arg.Any<ISaleIntegrationEventV1>(), cancellationToken)
            .Returns<Task>(_ => throw new InvalidOperationException("rabbit unavailable"));
        var dispatcher = CreateDispatcher(store, publisher);

        var processed = await dispatcher.DispatchBatchAsync(cancellationToken);

        Assert.Equal(1, processed);
        await store.Received(1).MarkFailedAsync(
            EventId,
            LockId,
            Now,
            Now.AddSeconds(60),
            "rabbit unavailable",
            false,
            cancellationToken);
        await store.DidNotReceive().MarkPublishedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_InvalidPayload_DeadLettersWithoutPublishing()
    {
        var store = Substitute.For<IOutboxStore>();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var cancellationToken = new CancellationTokenSource().Token;
        var poison = new ClaimedOutboxMessage(
            EventId,
            SalesIntegrationEventMapper.SaleCreatedType,
            "{not-json}",
            0,
            LockId);
        store.ClaimPendingAsync(10, Now, Now.AddSeconds(30), cancellationToken).Returns([poison]);
        var dispatcher = CreateDispatcher(store, publisher);

        var processed = await dispatcher.DispatchBatchAsync(cancellationToken);

        Assert.Equal(1, processed);
        await publisher.DidNotReceive().PublishAsync(Arg.Any<ISaleIntegrationEventV1>(), Arg.Any<CancellationToken>());
        await store.Received(1).MarkFailedAsync(
            EventId,
            LockId,
            Now,
            Now,
            Arg.Is<string>(error => error.Contains("not valid JSON", StringComparison.Ordinal)),
            true,
            cancellationToken);
    }

    [Fact]
    public async Task DispatchBatchAsync_MessageReclaimedAfterPublishOutcomeWasLost_ReusesStableEventId()
    {
        var store = Substitute.For<IOutboxStore>();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var claimed = CreateClaimedMessage();
        store.ClaimPendingAsync(10, Now, Now.AddSeconds(30), Arg.Any<CancellationToken>())
            .Returns([claimed], [claimed]);
        var publishedIds = new List<Guid>();
        publisher.PublishAsync(Arg.Any<ISaleIntegrationEventV1>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                publishedIds.Add(call.Arg<ISaleIntegrationEventV1>().EventId);
                return Task.CompletedTask;
            });
        store.MarkPublishedAsync(EventId, LockId, Now, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("simulated crash after publish")), Task.CompletedTask);
        var dispatcher = CreateDispatcher(store, publisher);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal([EventId, EventId], publishedIds);
    }

    private static OutboxDispatcher CreateDispatcher(
        IOutboxStore store,
        IIntegrationEventPublisher publisher) => new(
        store,
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

    private static ClaimedOutboxMessage CreateClaimedMessage(int attempts = 0)
    {
        var mapped = SalesIntegrationEventMapper.Map(
            new Ambev.DeveloperEvaluation.Domain.Sales.Events.SaleCreated(SaleId, Now),
            EventId,
            Now);
        return new ClaimedOutboxMessage(mapped.Id, mapped.Type, mapped.Payload, attempts, LockId);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
