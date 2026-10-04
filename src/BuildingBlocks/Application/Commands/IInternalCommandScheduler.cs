using ModularMonolith.BuildingBlocks.Application.Modules;

namespace ModularMonolith.BuildingBlocks.Application.Commands;

public interface IInternalCommandScheduler<TModule>
    where TModule : IModuleFacade
{
    void Enqueue(InternalCommand command);
}
