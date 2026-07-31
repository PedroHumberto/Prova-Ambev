namespace Ambev.DeveloperEvaluation.Contracts.Sales.V1;

public interface ISaleIntegrationEventV1
{
    Guid EventId { get; }

    Guid SaleId { get; }

    DateTime OccurredAt { get; }
}

public sealed record SaleCreatedV1(
    Guid EventId,
    Guid SaleId,
    DateTime OccurredAt) : ISaleIntegrationEventV1;

public sealed record SaleUpdatedV1(
    Guid EventId,
    Guid SaleId,
    DateTime OccurredAt) : ISaleIntegrationEventV1;

public sealed record SaleCancelledV1(
    Guid EventId,
    Guid SaleId,
    DateTime OccurredAt) : ISaleIntegrationEventV1;

public sealed record SaleItemCancelledV1(
    Guid EventId,
    Guid SaleId,
    Guid SaleItemId,
    DateTime OccurredAt) : ISaleIntegrationEventV1;
