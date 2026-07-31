using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Unit.TestInfrastructure;

public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLogEntry> _entries = [];

    public IReadOnlyList<RecordedLogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToDictionary(value => value.Key, value => value.Value)
            : new Dictionary<string, object?>();

        _entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception, properties));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

public sealed record RecordedLogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
