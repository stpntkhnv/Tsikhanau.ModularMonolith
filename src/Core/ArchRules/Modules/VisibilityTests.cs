using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class VisibilityTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Handlers_are_internal_and_sealed(String name) =>
        Check(
            VisibilityRules.HandlersAreInternalAndSealed(GetModule(name)),
            $"{name} has no handlers",
            "Declare the handler internal sealed");

    [Theory]
    [EveryModule]
    public void Module_exposes_at_most_its_module_class_outside_its_contracts(String name) =>
        Check(
            VisibilityRules.ModuleExposesAtMostItsModuleClassOutsideItsContracts(GetModule(name)),
            $"{name} has no types outside Contracts",
            "Make the type internal; outside Contracts only the module class is public");

    [Theory]
    [EveryModule]
    public void Module_assembly_exposes_only_its_module_class(String name) =>
        Check(
            VisibilityRules.ModuleAssemblyExposesOnlyItsModuleClass(GetModule(name)),
            $"{name} has no project assembly of its own",
            "Make the type internal; the module assembly shows only its module class");

    [Theory]
    [EveryModule]
    public void Contracts_are_public(String name) =>
        Check(
            VisibilityRules.ContractsArePublic(GetModule(name)),
            $"{name} has no contracts",
            "Make the contract type public; neighbours read it");

    [Theory]
    [EveryModule]
    public void Endpoints_are_internal_and_sealed(String name) =>
        Check(
            VisibilityRules.EndpointsAreInternalAndSealed(GetModule(name)),
            $"{name} has no endpoints",
            "Declare the endpoint internal sealed");
}
