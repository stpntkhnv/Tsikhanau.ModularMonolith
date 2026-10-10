using Tsikhanau.Monolith.Domain.Ids;

namespace Acme.Modules.Bad.Domain.Crates;

internal readonly record struct CrateId(Guid Value) : ITypedId<CrateId>
{
    public static CrateId From(Guid value) => new(value);
}
