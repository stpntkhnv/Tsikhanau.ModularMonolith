using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(String categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Clear() => _entries.Clear();

    public void Dispose() { }

    private static List<String> ReadValues(Object? state) =>
        state is IEnumerable<KeyValuePair<String, Object?>> values
            ? [.. values.Select(value => value.Value?.ToString() ?? String.Empty)]
            : [];

    private static void AddScope(Object? scope, List<String> scopes)
    {
        scopes.Add(scope?.ToString() ?? String.Empty);
        scopes.AddRange(ReadValues(scope));
    }

    private IDisposable PushScope(Object state) => _scopeProvider.Push(state);

    private void Record(String category, LogLevel level, String message, Object? state, Exception? exception)
    {
        var scopes = new List<String>();
        _scopeProvider.ForEachScope(AddScope, scopes);
        _entries.Enqueue(
            new LogEntry(
                category,
                level,
                message,
                ReadValues(state),
                scopes,
                exception?.ToString() ?? String.Empty));
    }

    private sealed class CapturingLogger(String categoryName, CapturingLoggerProvider capturingLoggerProvider)
        : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            capturingLoggerProvider.PushScope(state);

        public Boolean IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, String> formatter) =>
            capturingLoggerProvider.Record(categoryName, logLevel, formatter(state, exception), state, exception);
    }
}
