using Tsikhanau.Monolith.Domain.Ids;

namespace Acme.Modules.Bad.Domain.Pallets;

internal readonly record struct PalletId(Guid Value) : ITypedId<PalletId>
{
    public static PalletId From(Guid value) => new(value);
}
