using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.Application.Events;

public interface IOutbox<TModule>
    where TModule : IModuleFacade
{
    void Add(IntegrationEvent integrationEvent);
}
