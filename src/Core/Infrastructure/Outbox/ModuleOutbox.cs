using System.Diagnostics;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Outbox;

public sealed class ModuleOutbox<TModule>(
    ModuleContext<TModule> moduleContext,
    IntegrationEventRegistry integrationEventRegistry,
    TimeProvider timeProvider)
    : IOutbox<TModule>
    where TModule : IModuleFacade
{
    public void Add(IntegrationEvent integrationEvent)
    {
        var outboxMessage = OutboxMessage.Create(
            integrationEvent,
            integrationEventRegistry,
            timeProvider.GetUtcNow(),
            Activity.Current?.Id);

        moduleContext.DbContext
            .Set<OutboxMessage>()
            .Add(outboxMessage);
    }
}
