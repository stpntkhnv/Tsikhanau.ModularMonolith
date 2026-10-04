using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public static class TypedIdFactory<TId>
    where TId : struct, ITypedId<TId>
{
    public static TId Create(Guid value) => TId.From(value);
}
