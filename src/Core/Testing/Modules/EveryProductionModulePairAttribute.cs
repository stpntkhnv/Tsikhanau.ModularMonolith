using Xunit.v3;

namespace Tsikhanau.Monolith.Testing.Modules;

public sealed class EveryProductionModulePairAttribute : MemberDataAttributeBase
{
    public EveryProductionModulePairAttribute()
        : base(nameof(ProductionModules.Pairs), []) =>
        MemberType = typeof(ProductionModules);
}
