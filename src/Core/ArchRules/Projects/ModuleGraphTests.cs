using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class ModuleGraphTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Modules_do_not_reference_each_other_in_a_cycle() =>
        Check(
            ModuleGraphRules.ModulesDoNotReferenceEachOtherInACycle(Solution, Modules),
            "There are no production modules");

    [Fact]
    public void Modules_reference_only_contracts_of_known_modules() =>
        Check(
            ModuleGraphRules.ModulesReferenceOnlyContractsOfKnownModules(Solution, Modules),
            "There are no production modules");
}
