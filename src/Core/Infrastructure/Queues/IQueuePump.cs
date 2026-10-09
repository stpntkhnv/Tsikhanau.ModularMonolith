using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

public interface IQueuePump
{
    Task<Int32> RunOnceAsync(CancellationToken cancellationToken);
}

public interface IQueuePump<TModule> : IQueuePump
    where TModule : IModuleFacade;
