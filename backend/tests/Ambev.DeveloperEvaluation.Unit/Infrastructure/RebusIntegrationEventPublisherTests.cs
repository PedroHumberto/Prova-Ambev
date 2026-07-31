using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using NSubstitute;
using Rebus.Bus;
using Rebus.Messages;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public sealed class RebusIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SalesEvent_PublishesContractWithStableIdentityHeaders()
    {
        var eventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var integrationEvent = new SaleCreatedV1(
            eventId,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        var bus = Substitute.For<IBus>();
        var publisher = new RebusIntegrationEventPublisher(bus);

        await publisher.PublishAsync(integrationEvent, CancellationToken.None);

        await bus.Received(1).Publish(
            integrationEvent,
            Arg.Is<IDictionary<string, string>>(headers =>
                headers.Count == 4
                && headers[Headers.MessageId] == eventId.ToString("D")
                && headers[RebusIntegrationEventPublisher.EventIdHeader] == eventId.ToString("D")
                && headers[RebusIntegrationEventPublisher.EventTypeHeader] == nameof(SaleCreatedV1)
                && headers[RebusIntegrationEventPublisher.SchemaVersionHeader] == "1"));
    }

    [Fact]
    public async Task PublishAsync_CancellationRequested_DoesNotCallBus()
    {
        var bus = Substitute.For<IBus>();
        var publisher = new RebusIntegrationEventPublisher(bus);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var integrationEvent = new SaleCreatedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            publisher.PublishAsync(integrationEvent, cancellation.Token));

        await bus.DidNotReceive().Publish(Arg.Any<object>(), Arg.Any<IDictionary<string, string>>());
    }
}
