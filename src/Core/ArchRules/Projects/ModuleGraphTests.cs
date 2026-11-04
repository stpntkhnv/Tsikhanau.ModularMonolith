using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Projects.References;
using Tsikhanau.Monolith.Testing.Modules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class ModuleGraphTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Fact]
    public void Modules_do_not_reference_each_other_in_a_cycle() =>
        Check(
            ModuleGraphRules.ModulesDoNotReferenceEachOtherInACycle(Solution, Modules),
            ProductionModules.NoModuleReason,
            "Break the cycle: let one module handle the other's integration event instead of calling it");

    [Fact]
    public void Modules_reference_only_contracts_of_known_modules() =>
        Check(
            ModuleGraphRules.ModulesReferenceOnlyContractsOfKnownModules(Solution, Modules),
            ProductionModules.NoModuleReason,
            "Reference only the Contracts project of a module in src/Modules");
}
