using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Application.Events;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task<Result<Unit>> Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
