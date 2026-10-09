using Tsikhanau.Monolith.Domain.Events;

namespace Tsikhanau.Monolith.Domain.Aggregates;

public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
