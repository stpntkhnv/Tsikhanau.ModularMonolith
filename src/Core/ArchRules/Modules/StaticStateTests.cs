using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules.TypeDesign;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class StaticStateTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Module_types_hold_no_static_mutable_state(String name) =>
        Check(
            StaticStateRules.ModuleTypesHoldNoStaticMutableState(GetModule(name)),
            $"{name} has no types",
            "Keep state in instances that DI owns; static state leaks between requests and tests");
}
