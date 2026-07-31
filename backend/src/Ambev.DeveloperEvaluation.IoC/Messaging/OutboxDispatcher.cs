using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.Contracts.Sales.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.IoC.Messaging;

public sealed class OutboxDispatcher
{
    private const int MaximumStoredErrorLength = 2000;
    private readonly IOutboxStore _outboxStore;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly SalesMessagingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IOutboxStore outboxStore,
        IIntegrationEventPublisher publisher,
        IOptions<SalesMessagingOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxDispatcher> logger)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        var messages = await _outboxStore.ClaimPendingAsync(
            _options.OutboxBatchSize,
            now,
            now.AddSeconds(_options.LeaseSeconds),
            cancellationToken);

        foreach (var message in messages)
        {
            var integrationEvent = await DeserializeAsync(message, cancellationToken);
            if (integrationEvent is null)
                continue;

            try
            {
                await _publisher.PublishAsync(integrationEvent, cancellationToken);
                await _outboxStore.MarkPublishedAsync(
                    message.Id,
                    message.LockId,
                    GetUtcNow(),
                    cancellationToken);

                _logger.LogInformation(
                    "Published Sales integration event {EventType} with ID {EventId}",
                    message.Type,
                    message.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordPublishFailureAsync(message, exception, cancellationToken);
            }
        }

        return messages.Count;
    }

    private async Task<ISaleIntegrationEventV1?> DeserializeAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = SalesIntegrationEventMapper.Deserialize(message.Type, message.Payload);
            if (integrationEvent.EventId != message.Id)
            {
                throw new InvalidOutboxMessageException(
                    "Outbox payload event ID does not match its persisted message ID.");
            }

            return integrationEvent;
        }
        catch (InvalidOutboxMessageException exception)
        {
            await RecordPoisonMessageAsync(message, exception, cancellationToken);
            return null;
        }
    }

    private async Task RecordPublishFailureAsync(
        ClaimedOutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAt = GetUtcNow();
        var attempt = IncrementAttempt(message.Attempts);
        var nextAttemptAt = failedAt.Add(CalculateRetryDelay(attempt));

        await _outboxStore.MarkFailedAsync(
            message.Id,
            message.LockId,
            failedAt,
            nextAttemptAt,
            GetStoredError(exception),
            deadLetter: false,
            cancellationToken);

        _logger.LogWarning(
            exception,
            "Could not publish Sales integration event {EventType} with ID {EventId}; attempt {Attempt} will be retried at {NextAttemptAt}",
            message.Type,
            message.Id,
            attempt,
            nextAttemptAt);
    }

    private async Task RecordPoisonMessageAsync(
        ClaimedOutboxMessage message,
        InvalidOutboxMessageException exception,
        CancellationToken cancellationToken)
    {
        var failedAt = GetUtcNow();
        var attempt = IncrementAttempt(message.Attempts);

        await _outboxStore.MarkFailedAsync(
            message.Id,
            message.LockId,
            failedAt,
            failedAt,
            GetStoredError(exception),
            deadLetter: true,
            cancellationToken);

        _logger.LogError(
            exception,
            "Invalid Sales outbox message {EventType} with ID {EventId} was dead-lettered",
            message.Type,
            message.Id);
    }

    private TimeSpan CalculateRetryDelay(int attempt)
    {
        var exponent = attempt <= 1 ? 0 : Math.Min(attempt - 1, 20);
        var delaySeconds = _options.InitialRetryDelaySeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(delaySeconds, _options.MaximumRetryDelaySeconds));
    }

    private static int IncrementAttempt(int attempts) =>
        attempts == int.MaxValue ? int.MaxValue : attempts + 1;

    private static string GetStoredError(Exception exception)
    {
        var error = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        return error.Length > MaximumStoredErrorLength
            ? error[..MaximumStoredErrorLength]
            : error;
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
