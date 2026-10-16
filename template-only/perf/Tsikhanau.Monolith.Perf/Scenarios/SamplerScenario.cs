using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.Perf.Hosting;
using Tsikhanau.Monolith.Perf.Measuring;
using Tsikhanau.Monolith.Perf.Running;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class SamplerScenario : IScenario
{
    private const Int32 WarmupSamples = 5;

    private const String ReadScanCountsSql = $"""
        SELECT relname AS "TableName", indexrelname AS "IndexName", idx_scan AS "Scans"
        FROM pg_stat_user_indexes
        WHERE schemaname = '{PingsDbContext.SchemaName}'
        UNION ALL
        SELECT relname, 'sequential', seq_scan
        FROM pg_stat_user_tables
        WHERE schemaname = '{PingsDbContext.SchemaName}'
        """;

    private static readonly TimeSpan StatisticsFlushDelay = TimeSpan.FromSeconds(2);

    public String Name => "sampler";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var processedRows = options.GetInt32("processed", 1_000_000);
        var failedRows = options.GetInt32("failed", 10_000);
        var pendingRows = options.GetInt32("pending", 10_000);
        var samples = options.GetInt32("runs", 100);
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: false),
            cancellationToken);

        var seedTime = await SeedAsync(database, processedRows, failedRows, pendingRows, cancellationToken);
        Console.WriteLine(
            $"  every {PingsDbContext.SchemaName} queue table holds {processedRows} processed, {failedRows} failed "
                + $"and {pendingRows} due pending rows, seeded in {seedTime.TotalSeconds:F0} s; "
                + "the pongs tables are empty");

        var queueSampler = host.Services.GetRequiredService<QueueSampler>();
        var scanCountsBefore = await ReadScanCountsAsync(database, cancellationToken);
        await SampleAsync(queueSampler, WarmupSamples, cancellationToken);
        await database.ResetStatementsAsync(cancellationToken);
        var latency = await MeasureAsync(queueSampler, samples, cancellationToken);
        Console.WriteLine(BuildResultLine(samples, latency));
        await StatementReport.WriteAsync(database, samples, cancellationToken);
        ClearModulePools(host.Services);
        await Task.Delay(StatisticsFlushDelay, cancellationToken);
        var scanCountsAfter = await ReadScanCountsAsync(database, cancellationToken);
        QueueTable.OfPings
            .Select(queueTable => BuildScanLine(queueTable, WarmupSamples + samples, scanCountsBefore, scanCountsAfter))
            .ForEach(Console.WriteLine);

        await host.StopAsync(cancellationToken);
    }

    private static async Task<TimeSpan> SeedAsync(
        PerfDatabase database,
        Int32 processedRows,
        Int32 failedRows,
        Int32 pendingRows,
        CancellationToken cancellationToken)
    {
        var startedOn = Stopwatch.GetTimestamp();
        foreach (var queueTable in QueueTable.OfPings)
        {
            await database.ExecuteAsync(queueTable.BuildSeedSql(processedRows), cancellationToken);
            await database.ExecuteAsync(queueTable.BuildSeedFailedSql(failedRows), cancellationToken);
            await database.ExecuteAsync(queueTable.BuildSeedPendingSql(pendingRows), cancellationToken);
            await database.ExecuteAsync(queueTable.BuildVacuumSql(), cancellationToken);
        }

        return Stopwatch.GetElapsedTime(startedOn);
    }

    private static async Task SampleAsync(QueueSampler queueSampler, Int32 samples, CancellationToken cancellationToken)
    {
        for (var sample = 0; sample < samples; sample++)
        {
            await queueSampler.SampleAsync(cancellationToken);
        }
    }

    private static async Task<LatencySummary> MeasureAsync(
        QueueSampler queueSampler,
        Int32 samples,
        CancellationToken cancellationToken)
    {
        var latencyTicks = new Int64[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var sampleStartedOn = Stopwatch.GetTimestamp();
            await queueSampler.SampleAsync(cancellationToken);
            latencyTicks[sample] = Stopwatch.GetTimestamp() - sampleStartedOn;
        }

        return LatencySummary.FromTimestampTicks(latencyTicks);
    }

    private static async Task<IReadOnlyDictionary<(String TableName, String IndexName), Int64>> ReadScanCountsAsync(
        PerfDatabase database,
        CancellationToken cancellationToken)
    {
        var scanCounts = await database.QueryAsync<ScanCount>(ReadScanCountsSql, cancellationToken);
        return scanCounts.ToDictionary(
            scanCount => (scanCount.TableName, scanCount.IndexName),
            scanCount => scanCount.Scans);
    }

    private static String BuildResultLine(Int32 samples, LatencySummary latency) =>
        $"sampler n={samples} (2 modules, 3 queue tables each): p50 {latency.P50.TotalMilliseconds:F3} ms, "
            + $"p95 {latency.P95.TotalMilliseconds:F3} ms, p99 {latency.P99.TotalMilliseconds:F3} ms, "
            + $"mean {latency.Mean.TotalMilliseconds:F3} ms, load {MachineLoad.Read()}";

    private static void ClearModulePools(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ModuleRegistry>().Modules
            .Select(module => serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key))
            .ForEach(npgsqlDataSource => npgsqlDataSource.Clear());

    private static String BuildScanLine(
        QueueTable queueTable,
        Int32 samples,
        IReadOnlyDictionary<(String TableName, String IndexName), Int64> scanCountsBefore,
        IReadOnlyDictionary<(String TableName, String IndexName), Int64> scanCountsAfter)
    {
        var scans = scanCountsAfter
            .Where(scanCount => scanCount.Key.TableName == queueTable.Table)
            .OrderBy(scanCount => scanCount.Key.IndexName, StringComparer.Ordinal)
            .Select(scanCount => $"{scanCount.Key.IndexName} {scanCount.Value - scanCountsBefore[scanCount.Key]}");

        return $"  {queueTable.Name} scans in {samples} samples: {String.Join(", ", scans)}";
    }
}
