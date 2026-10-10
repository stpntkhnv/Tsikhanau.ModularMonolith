using Microsoft.Extensions.Logging;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Testing.App;

internal sealed class StartupFailureLoggerProvider : ILoggerProvider
{
    private Exception? _failure;

    public Optional<Exception> Failure => Optional<Exception>.FromNullable(Volatile.Read(ref _failure));

    public ILogger CreateLogger(String categoryName) => new StartupFailureLogger(this);

    public void Dispose() { }

    private void Record(Exception exception) => Volatile.Write(ref _failure, exception);

    private sealed class StartupFailureLogger(StartupFailureLoggerProvider startupFailureLoggerProvider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public Boolean IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Critical;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, String> formatter)
        {
            if (IsEnabled(logLevel) && exception is not null)
            {
                startupFailureLoggerProvider.Record(exception);
            }
        }
    }
}
