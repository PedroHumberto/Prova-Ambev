using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxStore : IOutboxStore
{
    private readonly DefaultContext _context;

    public OutboxStore(DefaultContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyCollection<ClaimedOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        DateTime now,
        DateTime lockedUntil,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var lockId = Guid.NewGuid();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var messages = await _context.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM "OutboxMessages"
                WHERE "PublishedAt" IS NULL
                  AND "DeadLetteredAt" IS NULL
                  AND "NextAttemptAt" <= {{now}}
                  AND ("LockedUntil" IS NULL OR "LockedUntil" <= {{now}})
                ORDER BY "CreatedAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {{batchSize}}
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
            message.Claim(lockId, lockedUntil);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages
            .Select(message => new ClaimedOutboxMessage(
                message.Id,
                message.Type,
                message.Payload,
                message.Attempts,
                lockId))
            .ToArray();
    }

    public async Task MarkPublishedAsync(
        Guid messageId,
        Guid lockId,
        DateTime publishedAt,
        CancellationToken cancellationToken)
    {
        var affectedRows = await _context.OutboxMessages
            .Where(message => message.Id == messageId
                && message.LockId == lockId
                && message.PublishedAt == null
                && message.DeadLetteredAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.PublishedAt, publishedAt)
                .SetProperty(message => message.LockId, (Guid?)null)
                .SetProperty(message => message.LockedUntil, (DateTime?)null)
                .SetProperty(message => message.LastError, (string?)null), cancellationToken);

        EnsureClaimWasOwned(affectedRows, messageId);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        Guid lockId,
        DateTime failedAt,
        DateTime nextAttemptAt,
        string error,
        bool deadLetter,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var deadLetteredAt = deadLetter ? failedAt : (DateTime?)null;
        var affectedRows = await _context.OutboxMessages
            .Where(message => message.Id == messageId
                && message.LockId == lockId
                && message.PublishedAt == null
                && message.DeadLetteredAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    message => message.Attempts,
                    message => message.Attempts == int.MaxValue
                        ? int.MaxValue
                        : message.Attempts + 1)
                .SetProperty(message => message.NextAttemptAt, nextAttemptAt)
                .SetProperty(message => message.DeadLetteredAt, deadLetteredAt)
                .SetProperty(message => message.LastError, error)
                .SetProperty(message => message.LockId, (Guid?)null)
                .SetProperty(message => message.LockedUntil, (DateTime?)null), cancellationToken);

        EnsureClaimWasOwned(affectedRows, messageId);
    }

    private static void EnsureClaimWasOwned(int affectedRows, Guid messageId)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Outbox message '{messageId}' is no longer owned by this dispatcher.");
        }
    }
}
