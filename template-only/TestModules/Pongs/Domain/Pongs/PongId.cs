using Tsikhanau.Monolith.Domain.Ids;

namespace Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

internal readonly record struct PongId(Guid Value) : ITypedId<PongId>
{
    public static PongId From(Guid value) => new(value);
}
