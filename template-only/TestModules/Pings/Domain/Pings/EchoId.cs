using Tsikhanau.Monolith.Domain.Ids;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

internal readonly record struct EchoId(Guid Value) : ITypedId<EchoId>
{
    public static EchoId From(Guid value) => new(value);
}
