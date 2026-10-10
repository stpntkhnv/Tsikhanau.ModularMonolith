using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class ProjectGraphTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Module_has_its_project_and_its_contracts(String name) =>
        Check(
            ProjectGraphRules.ModuleHasItsProjectAndItsContracts(Solution, GetModule(name)),
            $"{name} has no projects in src/Modules",
            "Make modules with bash scripts/scaffold.sh, which creates the project and its Contracts");

    [Theory]
    [EveryModule]
    public void Project_references_follow_the_layer_rules(String moduleName) =>
        Check(
            ProjectGraphRules.ProjectReferencesFollowTheLayerRules(Solution, Modules, GetModule(moduleName)),
            $"{moduleName} has no projects in src/Modules",
            "Drop the reference: a module project references only what its layer allows");
}
