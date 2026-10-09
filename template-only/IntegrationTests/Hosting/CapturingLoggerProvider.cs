using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(String categoryName) => new CapturingLogger(categoryName, _entries);

    public void Clear() => _entries.Clear();

    public void Dispose() { }

    private sealed class CapturingLogger(String categoryName, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public Boolean IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, String> formatter) =>
            entries.Enqueue(new LogEntry(categoryName, logLevel, formatter(state, exception)));
    }
}
