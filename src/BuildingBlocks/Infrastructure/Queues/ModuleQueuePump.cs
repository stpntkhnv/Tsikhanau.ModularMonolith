using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Inbox;
using ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;
using ModularMonolith.BuildingBlocks.Infrastructure.Outbox;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

public sealed class ModuleQueuePump<TModule>(
    OutboxPump<TModule> outboxPump,
    InboxPump<TModule> inboxPump,
    InternalCommandsPump<TModule> internalCommandsPump)
    : IQueuePump<TModule>
    where TModule : IModuleFacade
{
    public async Task<Int32> RunOnceAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        while (true)
        {
            var processedCount = await ProcessRoundAsync(cancellationToken);
            if (processedCount == 0)
            {
                return total;
            }

            total += processedCount;
        }
    }

    private async Task<Int32> ProcessRoundAsync(CancellationToken cancellationToken) =>
        Count(await outboxPump.TryProcessNextAsync(cancellationToken))
        + Count(await inboxPump.TryProcessNextAsync(cancellationToken))
        + Count(await internalCommandsPump.TryProcessNextAsync(cancellationToken));

    private static Int32 Count(Boolean isProcessed) => isProcessed ? 1 : 0;
}
