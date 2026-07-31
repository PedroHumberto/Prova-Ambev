namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTime occurredAt,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Outbox message ID cannot be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = EnsureUtc(occurredAt, nameof(occurredAt));
        CreatedAt = EnsureUtc(createdAt, nameof(createdAt));
        NextAttemptAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime NextAttemptAt { get; private set; }

    public int Attempts { get; private set; }

    public Guid? LockId { get; private set; }

    public DateTime? LockedUntil { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public DateTime? DeadLetteredAt { get; private set; }

    public string? LastError { get; private set; }

    public void Claim(Guid lockId, DateTime lockedUntil)
    {
        if (lockId == Guid.Empty)
            throw new ArgumentException("Outbox lock ID cannot be empty.", nameof(lockId));

        LockId = lockId;
        LockedUntil = EnsureUtc(lockedUntil, nameof(lockedUntil));
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The value must be a UTC instant.", parameterName);

        return value;
    }
}
