using ModularMonolith.BuildingBlocks.Domain.Events;
using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.BuildingBlocks.Domain.Aggregates;

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id), IAggregateRoot
    where TId : struct, ITypedId<TId>
{
    private readonly List<DomainEvent> _domainEvents = [];

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents;

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    void IAggregateRoot.ClearDomainEvents() => _domainEvents.Clear();
}
