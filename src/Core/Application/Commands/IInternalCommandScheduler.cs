using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Application.Commands;

public interface IInternalCommandScheduler<TModule>
    where TModule : IModuleFacade
{
    void Enqueue(InternalCommand command);
}
