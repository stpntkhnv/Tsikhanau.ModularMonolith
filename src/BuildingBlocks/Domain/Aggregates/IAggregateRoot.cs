using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.BuildingBlocks.Domain.Aggregates;

public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
