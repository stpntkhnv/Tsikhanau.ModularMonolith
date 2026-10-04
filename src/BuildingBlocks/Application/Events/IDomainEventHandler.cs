using ModularMonolith.BuildingBlocks.Domain.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Application.Events;

public interface IDomainEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task<Result<Unit>> Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
