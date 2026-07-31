using Ambev.DeveloperEvaluation.Contracts.Sales.V1;

namespace Ambev.DeveloperEvaluation.IoC.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(ISaleIntegrationEventV1 integrationEvent, CancellationToken cancellationToken);
}
