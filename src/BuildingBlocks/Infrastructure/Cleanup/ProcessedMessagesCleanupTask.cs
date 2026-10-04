using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Cleanup;

public sealed class ProcessedMessagesCleanupTask<TModule>(ModuleContext<TModule> moduleContext)
    : ICleanupTask<TModule>
    where TModule : IModuleFacade
{
    private const Int32 BatchSize = 1000;

    private static readonly String[] Tables =
    [
        OutboxMessage.Table,
        InboxMessage.Table,
        InternalCommandMessage.Table,
    ];

    public async Task<Int32> DeleteExpiredAsync(DateTimeOffset deleteBefore, CancellationToken cancellationToken)
    {
        var deletedCount = 0;
        foreach (var table in Tables)
        {
            deletedCount += await DeleteFromAsync(table, deleteBefore, cancellationToken);
        }

        return deletedCount;
    }

    private async Task<Int32> DeleteFromAsync(
        String table,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var tableDeletedCount = 0;
        Int32 batchDeletedCount;
        do
        {
            batchDeletedCount = await DeleteBatchAsync(table, deleteBefore, cancellationToken);
            tableDeletedCount += batchDeletedCount;
        }
        while (batchDeletedCount == BatchSize);

        return tableDeletedCount;
    }

    private Task<Int32> DeleteBatchAsync(
        String table,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var schema = moduleContext.Descriptor.Schema;
        var statement = $$"""
            DELETE FROM "{{schema}}".{{table}}
            WHERE id IN (SELECT id FROM "{{schema}}".{{table}} WHERE processed_on < {0} LIMIT {{BatchSize}})
            """;

        return moduleContext.DbContext.Database.ExecuteSqlRawAsync(statement, [deleteBefore], cancellationToken);
    }
}
