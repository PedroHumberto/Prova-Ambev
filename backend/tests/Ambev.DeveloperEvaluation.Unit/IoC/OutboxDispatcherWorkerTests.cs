using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.IoC;

public sealed class OutboxDispatcherWorkerTests
{
    [Fact]
    public async Task StartAsync_EmptyDispatchCycle_UsesScopeAndStopsOnHostCancellation()
    {
        var cycleCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Substitute.For<IOutboxStore>();
        store.ClaimPendingAsync(
                10,
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cycleCompleted.TrySetResult();
                return Task.FromResult<IReadOnlyCollection<ClaimedOutboxMessage>>([]);
            });
        var services = new ServiceCollection();
        services.AddScoped(_ => store);
        services.AddScoped(_ => Substitute.For<IIntegrationEventPublisher>());
        services.AddScoped<OutboxDispatcher>();
        services.AddSingleton<IOptions<SalesMessagingOptions>>(Options.Create(new SalesMessagingOptions
        {
            OutboxBatchSize = 10,
            PollingIntervalMilliseconds = 60000,
            LeaseSeconds = 30
        }));
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        var worker = new OutboxDispatcherWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<SalesMessagingOptions>>(),
            NullLogger<OutboxDispatcherWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await cycleCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        await store.Received(1).ClaimPendingAsync(
            10,
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        worker.Dispose();
    }
}
