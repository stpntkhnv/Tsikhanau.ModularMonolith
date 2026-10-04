using Xunit.v3;

namespace ModularMonolith.Testing.Modules;

public sealed class EveryProductionModuleAttribute : MemberDataAttributeBase
{
    public EveryProductionModuleAttribute()
        : base(nameof(ProductionModules.All), []) =>
        MemberType = typeof(ProductionModules);
}
