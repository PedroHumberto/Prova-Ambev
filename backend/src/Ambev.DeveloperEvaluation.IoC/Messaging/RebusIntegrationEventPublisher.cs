using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Rebus.Bus;
using Rebus.Messages;

namespace Ambev.DeveloperEvaluation.IoC.Messaging;

public sealed class RebusIntegrationEventPublisher : IIntegrationEventPublisher
{
    public const string EventIdHeader = "sales-event-id";
    public const string EventTypeHeader = "sales-event-type";
    public const string SchemaVersionHeader = "sales-event-schema-version";

    private readonly IBus _bus;

    public RebusIntegrationEventPublisher(IBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public Task PublishAsync(
        ISaleIntegrationEventV1 integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = integrationEvent.EventId.ToString("D"),
            [EventIdHeader] = integrationEvent.EventId.ToString("D"),
            [EventTypeHeader] = integrationEvent.GetType().Name,
            [SchemaVersionHeader] = "1"
        };

        return _bus.Publish(integrationEvent, headers);
    }
}
