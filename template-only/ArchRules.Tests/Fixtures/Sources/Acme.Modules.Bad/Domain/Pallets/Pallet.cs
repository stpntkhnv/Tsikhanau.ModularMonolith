using Acme.Modules.Bad.Domain.Crates;
using Tsikhanau.Monolith.Domain.Aggregates;

namespace Acme.Modules.Bad.Domain.Pallets;

internal sealed class Pallet : AggregateRoot<PalletId>
{
    private readonly List<Crate> _crates = [];

    private Pallet(PalletId id)
        : base(id)
    {
    }

    public Int32 CrateCount => _crates.Count;
}
