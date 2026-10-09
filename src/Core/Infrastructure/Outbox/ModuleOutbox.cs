using System.Diagnostics;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Infrastructure.EventBus;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues;

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
        var eventType = integrationEvent.GetType();
        integrationEventRegistry.Register(eventType);
        moduleContext.DbContext
            .Set<OutboxMessage>()
            .Add(new OutboxMessage
            {
                Id = integrationEvent.Id,
                Type = integrationEventRegistry.GetName(eventType),
                Payload = MessageSerializer.Serialize(integrationEvent),
                TraceParent = Activity.Current?.Id,
                NextAttemptOn = timeProvider.GetUtcNow(),
                OccurredOn = integrationEvent.OccurredOn,
            });
    }
}
