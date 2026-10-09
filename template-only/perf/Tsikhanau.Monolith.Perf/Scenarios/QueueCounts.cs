using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Perf.Database;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Perf.Scenarios;

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
