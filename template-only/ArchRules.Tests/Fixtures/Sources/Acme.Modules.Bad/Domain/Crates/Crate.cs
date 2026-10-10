using Tsikhanau.Monolith.Domain.Aggregates;

namespace Acme.Modules.Bad.Domain.Crates;

internal sealed class Crate : AggregateRoot<CrateId>
{
    public Crate(CrateId id)
        : base(id)
    {
    }
}
