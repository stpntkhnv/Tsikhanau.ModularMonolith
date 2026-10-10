using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class RuleCoverageTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Production_module_loads_its_project_and_its_contracts(String name) =>
        Check(
            CoverageRules.ProductionModuleLoadsItsProjectAndItsContracts(GetModule(name)),
            $"{name} has no assembly of its own",
            "Give the module exactly its project and its Contracts project, both built");
}
