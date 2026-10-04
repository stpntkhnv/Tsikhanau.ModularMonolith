using System.Diagnostics;
using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Infrastructure.EventBus;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Queues;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Outbox;

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
