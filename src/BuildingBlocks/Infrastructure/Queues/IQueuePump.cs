using ModularMonolith.BuildingBlocks.Application.Modules;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

public interface IQueuePump
{
    Task<Int32> RunOnceAsync(CancellationToken cancellationToken);
}

public interface IQueuePump<TModule> : IQueuePump
    where TModule : IModuleFacade;
