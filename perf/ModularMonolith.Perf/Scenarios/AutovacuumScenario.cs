using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Perf.Database;
using ModularMonolith.Perf.Hosting;
using ModularMonolith.Perf.Measuring;
using ModularMonolith.Perf.Running;
using ModularMonolith.Shared.Collections;
using ModularMonolith.TestModules.Pings;

namespace ModularMonolith.Perf.Scenarios;

internal sealed class AutovacuumScenario : IScenario
{
    private const Double BytesPerMegabyte = 1024 * 1024;

    public String Name => "autovacuum";

    public async Task RunAsync(PerfOptions options, CancellationToken cancellationToken)
    {
        var duration = TimeSpan.FromSeconds(options.GetInt32("seconds", 480));
        var sampleInterval = TimeSpan.FromSeconds(options.GetInt32("sample", 30));
        var rate = options.GetInt32("rate", 250);
        var parameters = AutovacuumParameters.From(options);
        var startedOn = Stopwatch.GetTimestamp();
        await using var database = await PerfDatabase.CreateAsync(cancellationToken);
        using var host = await PerfHost.StartAsync(
            database.ConnectionString,
            options.GetHostSettings(runInBackground: true),
            cancellationToken);

        var tableSettings = await PrepareTablesAsync(
            database,
            parameters,
            options.GetInt32("seed", 1_000_000),
            cancellationToken);

        Console.WriteLine($"  autovacuum: {parameters.Description}; rate {rate}/s for {duration.TotalSeconds:F0} s");
        var pingsModule = host.Services.GetRequiredService<IPingsModule>();
        var pingCreator = new PingCreator();
        var windows = await MeasureAsync(
            database,
            new ConstantRateLoad(rate, duration),
            token => pingCreator.CreateAsync(pingsModule, token),
            sampleInterval,
            cancellationToken);

        QueueTable.Watched.ForEach(queueTable => Console.WriteLine(BuildSummaryLine(queueTable, windows)));
        await RestoreTablesAsync(database, parameters, tableSettings, cancellationToken);
        await host.StopAsync(cancellationToken);
        var runTime = Stopwatch.GetElapsedTime(startedOn);
        Console.WriteLine($"  run time {runTime.TotalSeconds:F0} s, load {MachineLoad.Read()}");
    }

    private static async Task<IReadOnlyDictionary<QueueTable, String>> PrepareTablesAsync(
        PerfDatabase database,
        AutovacuumParameters parameters,
        Int32 seededRows,
        CancellationToken cancellationToken)
    {
        foreach (var queueTable in QueueTable.Watched)
        {
            await database.ExecuteAsync(queueTable.BuildSeedSql(seededRows), cancellationToken);
            await database.ExecuteAsync(queueTable.BuildVacuumSql(), cancellationToken);
        }

        var tableSettings = new Dictionary<QueueTable, String>();
        foreach (var queueTable in QueueTable.All)
        {
            tableSettings[queueTable] = await database.QuerySingleAsync<String>(
                queueTable.BuildReadSettingsSql(),
                cancellationToken);
        }

        if (!parameters.KeepTableSettings)
        {
            await ExecuteForAllAsync(database, parameters.BuildSetSql, cancellationToken);
        }

        return tableSettings;
    }

    private static async Task RestoreTablesAsync(
        PerfDatabase database,
        AutovacuumParameters parameters,
        IReadOnlyDictionary<QueueTable, String> tableSettings,
        CancellationToken cancellationToken)
    {
        if (parameters.KeepTableSettings)
        {
            return;
        }

        await ExecuteForAllAsync(database, parameters.BuildResetSql, cancellationToken);
        foreach (var (queueTable, settings) in tableSettings.Where(tableSetting => tableSetting.Value.Length > 0))
        {
            await database.ExecuteAsync(queueTable.BuildSetSettingsSql(settings), cancellationToken);
        }
    }

    private static async Task ExecuteForAllAsync(
        PerfDatabase database,
        Func<QueueTable, String> buildSql,
        CancellationToken cancellationToken)
    {
        foreach (var queueTable in QueueTable.All)
        {
            await database.ExecuteAsync(buildSql(queueTable), cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<SampleWindow>> MeasureAsync(
        PerfDatabase database,
        ConstantRateLoad load,
        Func<CancellationToken, Task> operation,
        TimeSpan sampleInterval,
        CancellationToken cancellationToken)
    {
        await database.ResetStatementsAsync(cancellationToken);
        var startedOn = Stopwatch.GetTimestamp();
        var previousSamples = await ReadSamplesAsync(database, cancellationToken);
        var windows = new List<SampleWindow>();
        var loadTask = load.RunAsync(operation, cancellationToken);
        while (!loadTask.IsCompleted)
        {
            await Task.WhenAny(loadTask, Task.Delay(sampleInterval, cancellationToken));
            var samples = await ReadSamplesAsync(database, cancellationToken);
            var endsAfter = Stopwatch.GetElapsedTime(startedOn);
            var window = new SampleWindow(endsAfter, load.SentCount, previousSamples, samples);
            Console.WriteLine(window.BuildLine());
            windows.Add(window);
            previousSamples = samples;
        }

        await loadTask;
        return windows;
    }

    private static async Task<IReadOnlyDictionary<QueueTable, QueueTableSample>> ReadSamplesAsync(
        PerfDatabase database,
        CancellationToken cancellationToken)
    {
        var samples = new Dictionary<QueueTable, QueueTableSample>();
        foreach (var queueTable in QueueTable.Watched)
        {
            samples[queueTable] = await database.QuerySingleAsync<QueueTableSample>(
                queueTable.BuildSampleSql(),
                cancellationToken);
        }

        return samples;
    }

    private static String BuildSummaryLine(QueueTable queueTable, IReadOnlyList<SampleWindow> windows)
    {
        var claimMilliseconds = windows
            .Select(window => window.GetMillisecondsPerClaim(queueTable))
            .ToList();

        var first = windows[0].Samples[queueTable];
        var last = windows[^1].Samples[queueTable];
        return $"{queueTable.Name}: autovacuums {last.Autovacuums}, "
            + $"ms per claim first {claimMilliseconds[0]:F3} last {claimMilliseconds[^1]:F3} "
            + $"max {claimMilliseconds.Max():F3} mean {claimMilliseconds.Average():F3}, "
            + $"dead rows max {windows.Max(window => window.Samples[queueTable].DeadRows)}, "
            + $"pending max {windows.Max(window => window.Samples[queueTable].PendingRows)}, "
            + $"indexes {first.IndexBytes / BytesPerMegabyte:F1} -> {last.IndexBytes / BytesPerMegabyte:F1} MB";
    }
}
