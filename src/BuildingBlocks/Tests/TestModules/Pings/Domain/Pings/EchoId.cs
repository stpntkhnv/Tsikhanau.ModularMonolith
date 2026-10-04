using ModularMonolith.BuildingBlocks.Domain.Ids;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal readonly record struct EchoId(Guid Value) : ITypedId<EchoId>
{
    public static EchoId From(Guid value) => new(value);
}
