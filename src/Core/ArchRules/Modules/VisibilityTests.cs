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
        Check(VisibilityRules.HandlersAreInternalAndSealed(GetModule(name)), $"{name} has no handlers");

    [Theory]
    [EveryModule]
    public void Module_exposes_at_most_its_module_class_outside_its_contracts(String name) =>
        Check(
            VisibilityRules.ModuleExposesAtMostItsModuleClassOutsideItsContracts(GetModule(name)),
            $"{name} has no types outside Contracts");

    [Theory]
    [EveryModule]
    public void Module_assembly_exposes_only_its_module_class(String name) =>
        Check(
            VisibilityRules.ModuleAssemblyExposesOnlyItsModuleClass(GetModule(name)),
            $"{name} has no project assembly of its own");

    [Theory]
    [EveryModule]
    public void Contracts_are_public(String name) =>
        Check(VisibilityRules.ContractsArePublic(GetModule(name)), $"{name} has no contracts");

    [Theory]
    [EveryModule]
    public void Endpoints_are_internal_and_sealed(String name) =>
        Check(VisibilityRules.EndpointsAreInternalAndSealed(GetModule(name)), $"{name} has no endpoints");
}
