namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public interface IOutboxStore
{
    Task<IReadOnlyCollection<ClaimedOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        DateTime now,
        DateTime lockedUntil,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        Guid messageId,
        Guid lockId,
        DateTime publishedAt,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid messageId,
        Guid lockId,
        DateTime failedAt,
        DateTime nextAttemptAt,
        string error,
        bool deadLetter,
        CancellationToken cancellationToken);
}

public sealed record ClaimedOutboxMessage(
    Guid Id,
    string Type,
    string Payload,
    int Attempts,
    Guid LockId);
