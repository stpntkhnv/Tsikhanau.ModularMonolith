using Xunit.v3;

namespace Tsikhanau.Monolith.Testing.Modules;

public sealed class EveryProductionModuleAttribute : MemberDataAttributeBase
{
    public EveryProductionModuleAttribute()
        : base(nameof(ProductionModules.All), []) =>
        MemberType = typeof(ProductionModules);
}
