using Xunit.v3;

namespace Tsikhanau.Monolith.ArchRules.Evaluation;

internal sealed class EveryModuleAttribute : MemberDataAttributeBase
{
    public EveryModuleAttribute()
        : base(nameof(ArchRuleTests<>.ModuleNames), [])
    {
    }
}
