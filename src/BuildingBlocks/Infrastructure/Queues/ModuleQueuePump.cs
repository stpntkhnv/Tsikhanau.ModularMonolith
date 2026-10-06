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
        await using var outboxBuffer = outboxPump.CreateBuffer();
        await using var inboxBuffer = inboxPump.CreateBuffer();
        await using var internalCommandsBuffer = internalCommandsPump.CreateBuffer();
        return await MessageBuffer.ProcessInRoundsAsync(
            [outboxBuffer, inboxBuffer, internalCommandsBuffer],
            cancellationToken);
    }
}
