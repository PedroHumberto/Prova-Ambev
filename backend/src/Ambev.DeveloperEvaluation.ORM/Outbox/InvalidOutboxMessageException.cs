namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class InvalidOutboxMessageException : Exception
{
    public InvalidOutboxMessageException(string message)
        : base(message)
    {
    }

    public InvalidOutboxMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
