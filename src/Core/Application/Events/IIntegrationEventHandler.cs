using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Events;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task<Result<Unit>> Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
