using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.TestModules.Pings;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class ModuleQueueProcessorTests
{
    private const String AbandonedHandlerWarning =
        "IPingsModule handler abandoned at shutdown; the message returns after its lease";

    private static readonly QueueSettings Settings = new()
    {
        PollInterval = TimeSpan.FromSeconds(5),
        HandlerTimeout = TimeSpan.FromMinutes(1),
        ClaimBatchSize = 16,
    };

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_warns_when_a_handler_outlives_the_shutdown_timeout()
    {
        var handlerReleaseSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuePump = new StubQueuePump(_ => handlerReleaseSource.Task);
        var logger = new CapturingLogger();
        using var moduleQueueProcessor = CreateProcessor(queuePump, logger);

        try
        {
            await moduleQueueProcessor.StartAsync(CancellationToken);
            await queuePump.Started.WaitAsync(Deadline, CancellationToken);
            await moduleQueueProcessor
                .StopAsync(new CancellationToken(canceled: true))
                .WaitAsync(Deadline, CancellationToken);
        }
        finally
        {
            handlerReleaseSource.TrySetResult();
        }

        logger.Entries.ShouldBe([new LogEntry(LogLevel.Warning, AbandonedHandlerWarning)]);
    }

    [Fact]
    public async Task Stop_stays_quiet_when_the_handler_stops_in_time()
    {
        var queuePump = new StubQueuePump(cancellationToken => Task.Delay(Timeout.Infinite, cancellationToken));
        var logger = new CapturingLogger();
        using var moduleQueueProcessor = CreateProcessor(queuePump, logger);

        await moduleQueueProcessor.StartAsync(CancellationToken);
        await queuePump.Started.WaitAsync(Deadline, CancellationToken);
        var stopping = moduleQueueProcessor.StopAsync(CancellationToken);
        await stopping.WaitAsync(Deadline, CancellationToken);

        logger.Entries.ShouldBeEmpty();
    }

    private static ModuleQueueProcessor<IPingsModule> CreateProcessor(
        IQueuePump<IPingsModule> queuePump,
        CapturingLogger logger) =>
        new(queuePump, new QueueSignals(), TimeProvider.System, Options.Create(Settings), logger);

    private sealed record LogEntry(LogLevel Level, String Message);

    private sealed class StubQueuePump(Func<CancellationToken, Task> handleAsync) : IQueuePump<IPingsModule>
    {
        private readonly TaskCompletionSource _startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _startedSource.Task;

        public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
        {
            _startedSource.TrySetResult();
            await handleAsync(cancellationToken);
            return 0;
        }
    }

    private sealed class CapturingLogger : ILogger<ModuleQueueProcessor<IPingsModule>>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries;

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
            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
    }
}
