using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.BuildingBlocks.Domain.Aggregates;

public abstract class Entity<TId>(TId id)
    where TId : struct, ITypedId<TId>
{
    public TId Id { get; } = id;
}
