namespace Ambev.DeveloperEvaluation.Domain.Sales.Events;

public sealed record SaleCreated(Guid SaleId, DateTime OccurredAt) : IDomainEvent;

public sealed record SaleUpdated(Guid SaleId, DateTime OccurredAt) : IDomainEvent;

public sealed record SaleCancelled(Guid SaleId, DateTime OccurredAt) : IDomainEvent;

public sealed record SaleItemCancelled(Guid SaleId, Guid SaleItemId, DateTime OccurredAt) : IDomainEvent;
