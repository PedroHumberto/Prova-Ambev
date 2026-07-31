using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Pipeline;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Messaging;

public sealed class RebusRabbitMqTransportTests
{
    [Fact]
    public async Task PublishAsync_ProvisionedRabbitMq_DeliversContractAndIdentityHeaders()
    {
        await using var rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await rabbitMq.StartAsync(CancellationToken.None);
        var received = new TaskCompletionSource<(SaleCreatedV1 Message, Dictionary<string, string> Headers)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var receiverActivator = new BuiltinHandlerActivator();
        receiverActivator.Handle<SaleCreatedV1>(message =>
        {
            received.TrySetResult((message, new Dictionary<string, string>(MessageContext.Current.Headers)));
            return Task.CompletedTask;
        });
        using var receiver = Configure.With(receiverActivator)
            .Transport(transport => transport.UseRabbitMq(
                rabbitMq.GetConnectionString(),
                $"sales-contract-test-{Guid.NewGuid():N}"))
            .Start();
        await receiver.Subscribe<SaleCreatedV1>();
        using var senderActivator = new BuiltinHandlerActivator();
        using var sender = Configure.With(senderActivator)
            .Transport(transport => transport.UseRabbitMqAsOneWayClient(rabbitMq.GetConnectionString()))
            .Start();
        var publisher = new RebusIntegrationEventPublisher(sender);
        var integrationEvent = new SaleCreatedV1(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));

        await publisher.PublishAsync(integrationEvent, CancellationToken.None);
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);

        Assert.Equal(integrationEvent, delivered.Message);
        Assert.Equal(integrationEvent.EventId.ToString("D"), delivered.Headers[Headers.MessageId]);
        Assert.Equal(
            integrationEvent.EventId.ToString("D"),
            delivered.Headers[RebusIntegrationEventPublisher.EventIdHeader]);
        Assert.Equal(nameof(SaleCreatedV1), delivered.Headers[RebusIntegrationEventPublisher.EventTypeHeader]);
        Assert.Equal("1", delivered.Headers[RebusIntegrationEventPublisher.SchemaVersionHeader]);
    }
}
