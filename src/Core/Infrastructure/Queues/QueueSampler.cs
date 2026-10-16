using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Shared.Time;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

internal sealed class QueueSampler(
    ModuleRegistry moduleRegistry,
    IServiceProvider serviceProvider,
    QueueMetrics queueMetrics,
    TimeProvider timeProvider,
    IOptions<QueueSettings> options,
    ILogger<QueueSampler> logger)
    : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(5);

    public Task SampleAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(moduleRegistry.Modules.Select(module => SampleWithinTimeoutAsync(module, cancellationToken)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunInBackground)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await SampleAsync(stoppingToken);
            await Task.Delay(SampleInterval, timeProvider, stoppingToken);
        }
    }

    private static String BuildSampleSql(String schema, String queue) =>
        $"""
        SELECT
            (SELECT count(*) FROM "{schema}".{queue}
             WHERE processed_on IS NULL AND failed_on IS NULL) AS "PendingCount",
            (SELECT count(*) FROM "{schema}".{queue}
             WHERE failed_on IS NOT NULL) AS "FailedCount",
            (SELECT min(next_attempt_on) FROM "{schema}".{queue}
             WHERE processed_on IS NULL AND failed_on IS NULL AND next_attempt_on <= @SampledOn) AS "OldestDueOn"
        """;

    private static async Task ObserveAbandonedAsync(Task abandonedReading)
    {
        try
        {
            await abandonedReading;
        }
        catch (Exception)
        {
        }
    }

    private async Task SampleWithinTimeoutAsync(ModuleDescriptor module, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(SampleTimeout, timeProvider);
        using var timeoutOrStopSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            cancellationToken);

        var reading = ReadAsync(module, timeoutOrStopSource.Token);
        try
        {
            queueMetrics.Store(await reading.WaitAsync(timeoutOrStopSource.Token));
        }
        catch (Exception exception)
        {
            _ = ObserveAbandonedAsync(reading);
            cancellationToken.ThrowIfCancellationRequested();
            LogFailure(module, exception, hasTimedOut: timeoutSource.IsCancellationRequested);
        }
    }

    private async Task<IReadOnlyList<QueueSample>> ReadAsync(
        ModuleDescriptor module,
        CancellationToken cancellationToken)
    {
        var sampledOn = timeProvider.GetUtcNow();
        await using var connection = await serviceProvider
            .GetRequiredKeyedService<NpgsqlDataSource>(module.Key)
            .OpenConnectionAsync(cancellationToken);

        var samples = new List<QueueSample>(QueueTables.Names.Count);
        foreach (var queue in QueueTables.Names)
        {
            var row = await connection.QuerySingleAsync<QueueSampleRow>(
                BuildCommand(module.Schema, queue, sampledOn, cancellationToken));

            samples.Add(row.ToSample(module.Name, queue));
        }

        return samples;
    }

    private static CommandDefinition BuildCommand(
        String schema,
        String queue,
        DateTimeOffset sampledOn,
        CancellationToken cancellationToken) =>
        new(
            BuildSampleSql(schema, queue),
            new { SampledOn = sampledOn },
            commandTimeout: SampleTimeout.TotalSecondsRoundedUp,
            cancellationToken: cancellationToken);

    private void LogFailure(ModuleDescriptor module, Exception exception, Boolean hasTimedOut)
    {
        if (hasTimedOut)
        {
            QueueSamplerLog.TimedOut(logger, module.Name, SampleTimeout);
            return;
        }

        QueueSamplerLog.Failed(logger, module.Name, exception);
    }
}
