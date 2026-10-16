using System.Diagnostics;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandScheduler<TModule>(
    ModuleContext<TModule> moduleContext,
    InternalCommandRegistry<TModule> internalCommandRegistry,
    TimeProvider timeProvider)
    : IInternalCommandScheduler<TModule>
    where TModule : IModuleFacade
{
    public void Enqueue(InternalCommand command)
    {
        var commandType = command.GetType();
        var enqueuedOn = timeProvider.GetUtcNow();
        moduleContext.DbContext
            .Set<InternalCommandMessage>()
            .Add(new InternalCommandMessage
            {
                Id = command.Id,
                Type = internalCommandRegistry.GetName(commandType),
                Payload = MessageSerializer.Serialize(command),
                ContainsSecrets = internalCommandRegistry.ContainsSecrets(commandType),
                TraceParent = Activity.Current?.Id,
                NextAttemptOn = enqueuedOn,
                EnqueuedOn = enqueuedOn,
            });
    }
}
