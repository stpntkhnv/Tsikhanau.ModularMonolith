using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
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

    [Theory]
    [EveryModule]
    public void Domain_types_have_no_public_fields(String name) =>
        Check(
            StaticStateRules.DomainTypesHaveNoPublicFields(GetModule(name)),
            $"{name} has no types in Domain",
            "Expose domain state through read-only properties");
}
