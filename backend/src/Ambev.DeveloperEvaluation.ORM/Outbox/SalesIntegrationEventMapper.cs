using System.Text.Json;
using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public static class SalesIntegrationEventMapper
{
    public const string SaleCreatedType = "sales.sale-created.v1";
    public const string SaleUpdatedType = "sales.sale-updated.v1";
    public const string SaleCancelledType = "sales.sale-cancelled.v1";
    public const string SaleItemCancelledType = "sales.sale-item-cancelled.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage Map(IDomainEvent domainEvent, Guid eventId, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var (type, integrationEvent) = domainEvent switch
        {
            SaleCreated created => (
                SaleCreatedType,
                (ISaleIntegrationEventV1)new SaleCreatedV1(eventId, created.SaleId, created.OccurredAt)),
            SaleUpdated updated => (
                SaleUpdatedType,
                new SaleUpdatedV1(eventId, updated.SaleId, updated.OccurredAt)),
            SaleCancelled cancelled => (
                SaleCancelledType,
                new SaleCancelledV1(eventId, cancelled.SaleId, cancelled.OccurredAt)),
            SaleItemCancelled itemCancelled => (
                SaleItemCancelledType,
                new SaleItemCancelledV1(
                    eventId,
                    itemCancelled.SaleId,
                    itemCancelled.SaleItemId,
                    itemCancelled.OccurredAt)),
            _ => throw new InvalidOperationException(
                $"Domain event '{domainEvent.GetType().FullName}' has no Sales integration-event mapping.")
        };

        return new OutboxMessage(
            eventId,
            type,
            JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
            domainEvent.OccurredAt,
            createdAt);
    }

    public static ISaleIntegrationEventV1 Deserialize(string type, string payload)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new InvalidOutboxMessageException("Outbox message type is empty.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidOutboxMessageException("Outbox message payload is empty.");

        try
        {
            ISaleIntegrationEventV1 integrationEvent = type switch
            {
                SaleCreatedType => Deserialize<SaleCreatedV1>(payload),
                SaleUpdatedType => Deserialize<SaleUpdatedV1>(payload),
                SaleCancelledType => Deserialize<SaleCancelledV1>(payload),
                SaleItemCancelledType => Deserialize<SaleItemCancelledV1>(payload),
                _ => throw new InvalidOutboxMessageException(
                    $"Outbox message type '{type}' is not supported.")
            };

            Validate(integrationEvent);
            return integrationEvent;
        }
        catch (JsonException exception)
        {
            throw new InvalidOutboxMessageException(
                $"Outbox payload for type '{type}' is not valid JSON for its contract.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidOutboxMessageException(
                $"Outbox payload for type '{type}' cannot be deserialized.",
                exception);
        }
    }

    private static T Deserialize<T>(string payload) where T : ISaleIntegrationEventV1 =>
        JsonSerializer.Deserialize<T>(payload, SerializerOptions)
        ?? throw new InvalidOutboxMessageException(
            $"Outbox payload for '{typeof(T).Name}' cannot be null.");

    private static void Validate(ISaleIntegrationEventV1 integrationEvent)
    {
        if (integrationEvent.EventId == Guid.Empty)
            throw new InvalidOutboxMessageException("Outbox payload contains an empty event ID.");

        if (integrationEvent.SaleId == Guid.Empty)
            throw new InvalidOutboxMessageException("Outbox payload contains an empty sale ID.");

        if (integrationEvent.OccurredAt.Kind != DateTimeKind.Utc)
            throw new InvalidOutboxMessageException("Outbox payload occurrence time is not UTC.");

        if (integrationEvent is SaleItemCancelledV1 { SaleItemId: var saleItemId }
            && saleItemId == Guid.Empty)
        {
            throw new InvalidOutboxMessageException("Outbox payload contains an empty sale item ID.");
        }
    }
}
