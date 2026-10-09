using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class QueueBackgroundScenario : IScenario
{
    private static readonly TimeSpan CountInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromMinutes(10);

    public String Name => "queue-background";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var messages = options.GetInt32("n", 2000);
        var instances = options.GetInt32("instances", 1);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        var pingCreator = new PingCreator();
        await WarmAsync(database, options, pingCreator, cancellationToken);
        await SeedAsync(database, options, pingCreator, messages, cancellationToken);
        var hosts = await PrepareHostsAsync(database, options, instances, cancellationToken);
        try
        {
            await MeasureAsync(database, hosts, messages, cancellationToken);
        }
        finally
        {
            await StopAsync(hosts);
        }
    }

    private static async Task WarmAsync(
        PerfDatabase database,
        PerfOptions options,
        PingCreator pingCreator,
        CancellationToken cancellationToken)
    {
        using var host = await StartSeedingHostAsync(database, options, cancellationToken);
        await QueueDriver.SeedAsync(host, pingCreator, QueueDriver.WarmupMessages, cancellationToken);
        await QueueDriver.DeliverPingsOutboxAsync(host, cancellationToken);
        await QueueDriver.ProcessPongsInboxAsync(host, cancellationToken);
        await host.StopAsync(cancellationToken);
    }

    private static async Task SeedAsync(
        PerfDatabase database,
        PerfOptions options,
        PingCreator pingCreator,
        Int32 messages,
        CancellationToken cancellationToken)
    {
        using var host = await StartSeedingHostAsync(database, options, cancellationToken);
        await QueueDriver.SeedAsync(host, pingCreator, messages, cancellationToken);
        await host.StopAsync(cancellationToken);
    }

    private static Task<IHost> StartSeedingHostAsync(
        PerfDatabase database,
        PerfOptions options,
        CancellationToken cancellationToken) =>
        PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

    private static async Task<IReadOnlyList<IHost>> PrepareHostsAsync(
        PerfDatabase database,
        PerfOptions options,
        Int32 instances,
        CancellationToken cancellationToken)
    {
        var hosts = new List<IHost>();
        for (var instance = 0; instance < instances; instance++)
        {
            var host = PerfHost.Build(database.ConnectionString, options.GetHostSettings(runInBackground: true));
            hosts.Add(host);
            await host.Services.PrepareModuleDatabasesAsync(cancellationToken);
        }

        return hosts;
    }

    private static async Task MeasureAsync(
        PerfDatabase database,
        IReadOnlyList<IHost> hosts,
        Int32 messages,
        CancellationToken cancellationToken)
    {
        var countsBefore = await QueueCounts.ReadAsync(database, cancellationToken);
        await database.ResetStatementsAsync(cancellationToken);
        GC.Collect();
        var start = ResourceSnapshot.Take();
        await StartAsync(hosts, cancellationToken);
        var outboxDrainedAfter = await WaitForAllPongsAsync(database, countsBefore, messages, start, cancellationToken);
        var usage = ResourceUsage.Since(start);
        Console.WriteLine(BuildResultLine(hosts.Count, messages, outboxDrainedAfter, usage));
        await StatementReport.WriteAsync(database, messages, cancellationToken);
    }

    private static async Task StartAsync(IReadOnlyList<IHost> hosts, CancellationToken cancellationToken)
    {
        foreach (var host in hosts)
        {
            await host.StartAsync(cancellationToken);
        }
    }

    private static async Task<TimeSpan> WaitForAllPongsAsync(
        PerfDatabase database,
        QueueCounts countsBefore,
        Int32 messages,
        ResourceSnapshot start,
        CancellationToken cancellationToken)
    {
        Optional<TimeSpan> outboxDrainedAfter = Optional<TimeSpan>.None();
        while (true)
        {
            var counts = await QueueCounts.ReadAsync(database, cancellationToken);
            var progress = counts.Since(countsBefore);
            var elapsed = Stopwatch.GetElapsedTime(start.Timestamp);
            if (outboxDrainedAfter.IsNone && progress.ProcessedOutboxMessages >= messages)
            {
                outboxDrainedAfter = elapsed;
            }

            if (progress.Pongs >= messages)
            {
                return outboxDrainedAfter.GetValueOrDefault(elapsed);
            }

            if (elapsed > DrainTimeout)
            {
                throw new TimeoutException($"outbox {progress.ProcessedOutboxMessages} pongs {progress.Pongs}");
            }

            await Task.Delay(CountInterval, cancellationToken);
        }
    }

    private static String BuildResultLine(
        Int32 instances,
        Int32 messages,
        TimeSpan outboxDrainedAfter,
        ResourceUsage usage) =>
        $"background x{instances}: n={messages} outbox drained {outboxDrainedAfter.TotalSeconds:F2} s "
            + $"({messages / outboxDrainedAfter.TotalSeconds:F0}/s), all pongs {usage.Elapsed.TotalSeconds:F2} s "
            + $"({messages / usage.Elapsed.TotalSeconds:F0} events/s end to end), "
            + $"alloc {usage.AllocatedBytes / (Double)messages:F0} B/event, "
            + $"cpu {usage.ProcessorTime.TotalMicroseconds / messages:F0} us/event, load {MachineLoad.Read()}";

    private static async Task StopAsync(IReadOnlyList<IHost> hosts)
    {
        foreach (var host in hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
    }
}
