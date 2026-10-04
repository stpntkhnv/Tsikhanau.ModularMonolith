using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

public sealed class InternalCommandScheduler<TModule>(ModuleContext<TModule> moduleContext, TimeProvider timeProvider)
    : IInternalCommandScheduler<TModule>
    where TModule : IModuleFacade
{
    public void Enqueue(InternalCommand command)
    {
        var enqueuedOn = timeProvider.GetUtcNow();
        moduleContext.DbContext
            .Set<InternalCommandMessage>()
            .Add(new InternalCommandMessage
            {
                Id = command.Id,
                Type = InternalCommandRegistry<TModule>.GetName(command.GetType()),
                Payload = MessageSerializer.Serialize(command),
                TraceParent = Activity.Current?.Id,
                NextAttemptOn = enqueuedOn,
                EnqueuedOn = enqueuedOn,
            });
    }
}
