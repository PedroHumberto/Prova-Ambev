namespace Ambev.DeveloperEvaluation.Domain.Sales.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
