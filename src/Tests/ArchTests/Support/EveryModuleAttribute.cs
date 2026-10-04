using Xunit.v3;

namespace ModularMonolith.ArchTests.Support;

public sealed class EveryModuleAttribute : MemberDataAttributeBase
{
    public EveryModuleAttribute()
        : base(nameof(ArchModules.All), []) =>
        MemberType = typeof(ArchModules);
}
