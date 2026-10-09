using Tsikhanau.Monolith.Domain.Ids;

namespace Tsikhanau.Monolith.Domain.Aggregates;

public abstract class Entity<TId>(TId id)
    where TId : struct, ITypedId<TId>
{
    public TId Id { get; } = id;
}
