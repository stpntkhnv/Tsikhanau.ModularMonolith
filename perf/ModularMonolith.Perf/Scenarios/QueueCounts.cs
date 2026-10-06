using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;
using ModularMonolith.Perf.Database;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.Perf.Scenarios;

internal readonly record struct QueueCounts(Int64 ProcessedOutboxMessages, Int64 Pongs)
{
    private const String CountProcessedOutboxSql =
        $"SELECT count(*) FROM {PingsDbContext.SchemaName}.{OutboxMessage.Table} WHERE processed_on IS NOT NULL";

    private const String CountPongsSql = $"SELECT count(*) FROM {PongsDbContext.SchemaName}.pongs";

    public static async Task<QueueCounts> ReadAsync(PerfDatabase database, CancellationToken cancellationToken)
    {
        var processedOutboxMessages = await database.CountAsync(CountProcessedOutboxSql, cancellationToken);
        var pongs = await database.CountAsync(CountPongsSql, cancellationToken);
        return new QueueCounts(processedOutboxMessages, pongs);
    }

    public QueueCounts Since(QueueCounts start) =>
        new(ProcessedOutboxMessages - start.ProcessedOutboxMessages, Pongs - start.Pongs);
}
