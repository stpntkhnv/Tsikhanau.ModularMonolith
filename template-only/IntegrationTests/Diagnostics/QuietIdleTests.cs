using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularMonolith.Testing.Database;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Diagnostics;

public sealed class QuietIdleTests(PostgresFixture postgresFixture)
{
    private const Int32 IdlePollCount = 3;

    private static readonly TimeSpan IdleDeadline = TimeSpan.FromSeconds(15);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Idle_queues_write_no_logs_at_information_and_trace_no_database_command_without_a_parent()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        var pingsPollCounter = new PollCounter();
        var pongsPollCounter = new PollCounter();
        PollCounter[] pollCounters = [pingsPollCounter, pongsPollCounter];
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            runInBackground: true,
            configure: builder =>
            {
                HostLoggingSettings
                    .ApplyTo(builder.Logging)
                    .AddProvider(capturingLoggerProvider);

                CountPolls<IPingsModule>(builder.Services, pingsPollCounter);
                CountPolls<IPongsModule>(builder.Services, pongsPollCounter);
            });

        using var npgsqlActivities = new NpgsqlActivities();
        capturingLoggerProvider.Clear();
        pollCounters.ForEach(pollCounter => pollCounter.Reset());

        await AdvanceUntilPolledAsync(testModulesHost, pollCounters);

        var entriesAtInformationOrAbove = capturingLoggerProvider.Entries
            .Where(entry => entry.Level >= LogLevel.Information)
            .ToList();

        entriesAtInformationOrAbove.ShouldBeEmpty();
        npgsqlActivities.Unparented.ShouldBeEmpty();
    }

    [Fact]
    public async Task Database_command_within_an_activity_is_still_traced()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var npgsqlActivities = new NpgsqlActivities();

        await testModulesHost.CreatePingAsync("traced");

        npgsqlActivities.Parented.ShouldNotBeEmpty();
    }

    private static void CountPolls<TModule>(IServiceCollection services, PollCounter pollCounter)
        where TModule : IModuleFacade =>
        services.AddSingleton<IQueuePump<TModule>>(
            serviceProvider => new CountingQueuePump<TModule>(
                ActivatorUtilities.CreateInstance<ModuleQueuePump<TModule>>(serviceProvider),
                pollCounter));

    private static async Task AdvanceUntilPolledAsync(
        TestModulesHost testModulesHost,
        IReadOnlyList<PollCounter> pollCounters)
    {
        var pollInterval = testModulesHost.Services.GetRequiredService<IOptions<QueueSettings>>().Value.PollInterval;
        var stopwatch = Stopwatch.StartNew();
        while (pollCounters.Any(pollCounter => pollCounter.Count < IdlePollCount))
        {
            stopwatch.Elapsed.ShouldBeLessThan(IdleDeadline);
            testModulesHost.Time.Advance(pollInterval);
            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }
    }

    private sealed class PollCounter
    {
        private Int32 _count;

        public Int32 Count => Volatile.Read(ref _count);

        public void Add() => Interlocked.Increment(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);
    }

    private sealed class CountingQueuePump<TModule>(IQueuePump<TModule> queuePump, PollCounter pollCounter)
        : IQueuePump<TModule>
        where TModule : IModuleFacade
    {
        public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
        {
            var processedCount = await queuePump.RunOnceAsync(cancellationToken);
            pollCounter.Add();
            return processedCount;
        }
    }

    private sealed class NpgsqlActivities : IDisposable
    {
        private const String NpgsqlSourceName = "Npgsql";

        private readonly ConcurrentQueue<Activity> _activities = new();
        private readonly ActivityListener _activityListener;

        public NpgsqlActivities()
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = activitySource => activitySource.Name is NpgsqlSourceName or AppDiagnostics.SourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = Record,
            };

            ActivitySource.AddActivityListener(_activityListener);
        }

        public IReadOnlyList<String> Unparented =>
            _activities
                .Where(activity => activity.ParentId is null)
                .Select(activity => activity.DisplayName)
                .ToList();

        public IReadOnlyList<String> Parented =>
            _activities
                .Where(activity => activity.ParentId is not null)
                .Select(activity => activity.DisplayName)
                .ToList();

        public void Dispose() => _activityListener.Dispose();

        private void Record(Activity activity)
        {
            if (activity.Source.Name == NpgsqlSourceName)
            {
                _activities.Enqueue(activity);
            }
        }
    }
}
