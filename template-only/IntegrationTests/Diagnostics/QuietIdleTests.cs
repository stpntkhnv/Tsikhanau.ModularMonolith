using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pongs;

namespace Tsikhanau.Monolith.IntegrationTests.Diagnostics;

public sealed class QuietIdleTests(PostgresFixture postgresFixture)
{
    private const Int32 IdlePollCount = 3;

    private const Int32 ModuleQueueCount = TestModuleCount * QueuesPerModule;

    private const Int32 TestModuleCount = 2;

    private const Int32 QueuesPerModule = 3;

    private const String PendingInstrument = "tsikhanau.queue.pending";

    private const String FailedInstrument = "tsikhanau.queue.failed";

    private const String InsertFailedInboxRowSql = """
        INSERT INTO pongs.inbox_messages (id, type, payload, attempts, next_attempt_on, occurred_on, received_on, failed_on)
        VALUES (@Id, 'idle.failed', '{}'::jsonb, 7, @FailedOn, @FailedOn, @FailedOn, @FailedOn)
        """;

    private static readonly TimeSpan IdleDeadline = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

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

        using var metricRecorder = MetricRecorder.StartFor(testModulesHost.Services);
        await WaitUntilSampledAtStartAsync(metricRecorder);
        await InsertFailedInboxRowAsync(testModulesHost);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder();
        npgsqlCommandRecorder.Start();
        capturingLoggerProvider.Clear();
        pollCounters.ForEach(pollCounter => pollCounter.Reset());

        await AdvanceUntilPolledAndSampledAsync(testModulesHost, pollCounters, metricRecorder);
        npgsqlCommandRecorder.Stop();

        var entriesAtInformationOrAbove = capturingLoggerProvider.Entries
            .Where(entry => entry.Level >= LogLevel.Information)
            .ToList();

        entriesAtInformationOrAbove.ShouldBeEmpty();
        npgsqlCommandRecorder.Unparented.ShouldBeEmpty();
    }

    [Fact]
    public async Task Database_command_within_an_activity_is_still_traced()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var npgsqlCommandRecorder = new NpgsqlCommandRecorder();
        npgsqlCommandRecorder.Start();

        await testModulesHost.CreatePingAsync("traced");
        npgsqlCommandRecorder.Stop();

        npgsqlCommandRecorder.Parented.ShouldNotBeEmpty();
    }

    private static void CountPolls<TModule>(IServiceCollection services, PollCounter pollCounter)
        where TModule : IModuleFacade =>
        services.AddSingleton<IQueuePump<TModule>>(
            serviceProvider => new CountingQueuePump<TModule>(
                ActivatorUtilities.CreateInstance<ModuleQueuePump<TModule>>(serviceProvider),
                pollCounter));

    private static async Task WaitUntilSampledAtStartAsync(MetricRecorder metricRecorder)
    {
        var stopwatch = Stopwatch.StartNew();
        while (metricRecorder.Observe(PendingInstrument).Count < ModuleQueueCount)
        {
            stopwatch.Elapsed.ShouldBeLessThan(IdleDeadline);
            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }
    }

    private static Task InsertFailedInboxRowAsync(TestModulesHost testModulesHost) =>
        testModulesHost.Database.ExecuteAsync(
            InsertFailedInboxRowSql,
            new { Id = Guid.CreateVersion7(), FailedOn = testModulesHost.Time.GetUtcNow() });

    private static async Task AdvanceUntilPolledAndSampledAsync(
        TestModulesHost testModulesHost,
        IReadOnlyList<PollCounter> pollCounters,
        MetricRecorder metricRecorder)
    {
        var pollInterval = testModulesHost.Services.GetRequiredService<IOptions<QueueSettings>>().Value.PollInterval;
        var advancedBy = TimeSpan.Zero;
        var stopwatch = Stopwatch.StartNew();
        while (pollCounters.Any(pollCounter => pollCounter.Count < IdlePollCount) || !HasSampled(metricRecorder))
        {
            stopwatch.Elapsed.ShouldBeLessThan(IdleDeadline);
            if (advancedBy < SampleInterval || HasSampled(metricRecorder))
            {
                testModulesHost.Time.Advance(pollInterval);
                advancedBy += pollInterval;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }
    }

    private static Boolean HasSampled(MetricRecorder metricRecorder) =>
        metricRecorder
            .Observe(FailedInstrument)
            .Any(IsTheFailedInboxRow);

    private static Boolean IsTheFailedInboxRow(RecordedMeasurement measurement) =>
        measurement.Tags["module"] == "Pongs"
        && measurement.Tags["queue"] == "inbox_messages"
        && measurement.Value == 1;

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
}
