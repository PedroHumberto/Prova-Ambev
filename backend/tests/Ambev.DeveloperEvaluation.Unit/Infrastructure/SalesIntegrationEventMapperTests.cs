using System.Text.Json;
using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

public sealed class SalesIntegrationEventMapperTests
{
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SaleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SaleItemId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTime OccurredAt = new(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CreatedAt = new(2026, 7, 31, 10, 0, 1, DateTimeKind.Utc);

    public static TheoryData<IDomainEvent, string, Type, string[]> Mappings => new()
    {
        {
            new SaleCreated(SaleId, OccurredAt),
            SalesIntegrationEventMapper.SaleCreatedType,
            typeof(SaleCreatedV1),
            ["eventId", "saleId", "occurredAt"]
        },
        {
            new SaleUpdated(SaleId, OccurredAt),
            SalesIntegrationEventMapper.SaleUpdatedType,
            typeof(SaleUpdatedV1),
            ["eventId", "saleId", "occurredAt"]
        },
        {
            new SaleCancelled(SaleId, OccurredAt),
            SalesIntegrationEventMapper.SaleCancelledType,
            typeof(SaleCancelledV1),
            ["eventId", "saleId", "occurredAt"]
        },
        {
            new SaleItemCancelled(SaleId, SaleItemId, OccurredAt),
            SalesIntegrationEventMapper.SaleItemCancelledType,
            typeof(SaleItemCancelledV1),
            ["eventId", "saleId", "saleItemId", "occurredAt"]
        }
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void Map_SupportedSalesEvent_ProducesMinimalVersionedContract(
        IDomainEvent domainEvent,
        string expectedMessageType,
        Type expectedContractType,
        string[] expectedPayloadProperties)
    {
        var message = SalesIntegrationEventMapper.Map(domainEvent, EventId, CreatedAt);
        var integrationEvent = SalesIntegrationEventMapper.Deserialize(message.Type, message.Payload);
        using var payload = JsonDocument.Parse(message.Payload);

        Assert.Equal(EventId, message.Id);
        Assert.Equal(expectedMessageType, message.Type);
        Assert.Equal(OccurredAt, message.OccurredAt);
        Assert.Equal(CreatedAt, message.CreatedAt);
        Assert.IsType(expectedContractType, integrationEvent);
        Assert.Equal(EventId, integrationEvent.EventId);
        Assert.Equal(SaleId, integrationEvent.SaleId);
        Assert.Equal(OccurredAt, integrationEvent.OccurredAt);
        Assert.Equal(
            expectedPayloadProperties.OrderBy(property => property),
            payload.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(property => property));

        if (integrationEvent is SaleItemCancelledV1 itemCancelled)
            Assert.Equal(SaleItemId, itemCancelled.SaleItemId);
    }

    [Fact]
    public void ContractsAssembly_ExternalSalesContracts_HasNoEntityFrameworkDependency()
    {
        var references = typeof(ISaleIntegrationEventV1).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference =>
            reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            || reference.Name?.StartsWith("Npgsql", StringComparison.Ordinal) == true);
    }
}
