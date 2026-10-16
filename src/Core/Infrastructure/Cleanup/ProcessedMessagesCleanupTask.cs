using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;

namespace Tsikhanau.Monolith.Infrastructure.Cleanup;

public sealed class ProcessedMessagesCleanupTask<TModule>(
    ModuleContext<TModule> moduleContext,
    IOptions<CleanupSettings> options,
    TimeProvider timeProvider)
    : ICleanupTask<TModule>
    where TModule : IModuleFacade
{
    private const Int32 BatchSize = 1000;

    private const String ProcessedOn = "processed_on";

    private const String FailedOn = "failed_on";

    private static readonly String[] Tables =
    [
        OutboxMessage.Table,
        InboxMessage.Table,
        InternalCommandMessage.Table,
    ];

    public async Task<Int32> DeleteExpiredAsync(DateTimeOffset deleteBefore, CancellationToken cancellationToken)
    {
        var deleteFailedBefore = timeProvider.GetUtcNow() - options.Value.KeepFailedFor;
        var deletedCount = 0;
        foreach (var table in Tables)
        {
            deletedCount += await DeleteFromAsync(table, ProcessedOn, deleteBefore, cancellationToken);
            deletedCount += await DeleteFromAsync(table, FailedOn, deleteFailedBefore, cancellationToken);
        }

        return deletedCount;
    }

    private async Task<Int32> DeleteFromAsync(
        String table,
        String endedOnColumn,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var tableDeletedCount = 0;
        Int32 batchDeletedCount;
        do
        {
            batchDeletedCount = await DeleteBatchAsync(table, endedOnColumn, deleteBefore, cancellationToken);
            tableDeletedCount += batchDeletedCount;
        }
        while (batchDeletedCount == BatchSize);

        return tableDeletedCount;
    }

    private Task<Int32> DeleteBatchAsync(
        String table,
        String endedOnColumn,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var schema = moduleContext.Descriptor.Schema;
        var statement = $$"""
            DELETE FROM "{{schema}}".{{table}}
            WHERE id IN (SELECT id FROM "{{schema}}".{{table}} WHERE {{endedOnColumn}} < {0} LIMIT {{BatchSize}})
                AND {{endedOnColumn}} < {0}
            """;

        return moduleContext.DbContext.Database.ExecuteSqlRawAsync(statement, [deleteBefore], cancellationToken);
    }
}
