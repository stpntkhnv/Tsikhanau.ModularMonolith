using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.TestModules.Pongs.Domain.Pongs;

internal readonly record struct PongId(Guid Value) : ITypedId<PongId>
{
    public static PongId From(Guid value) => new(value);
}
