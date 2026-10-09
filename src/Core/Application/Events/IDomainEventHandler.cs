using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Events;

public interface IDomainEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task<Result<Unit>> Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
