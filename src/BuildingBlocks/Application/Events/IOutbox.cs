using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

namespace ModularMonolith.BuildingBlocks.Application.Events;

public interface IOutbox<TModule>
    where TModule : IModuleFacade
{
    void Add(IntegrationEvent integrationEvent);
}
