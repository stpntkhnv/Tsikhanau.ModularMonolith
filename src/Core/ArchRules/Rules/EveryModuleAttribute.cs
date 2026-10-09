using Xunit.v3;

namespace Tsikhanau.Monolith.ArchRules.Rules;

internal sealed class EveryModuleAttribute : MemberDataAttributeBase
{
    public EveryModuleAttribute()
        : base(nameof(ArchRuleTests<>.ModuleNames), [])
    {
    }
}
