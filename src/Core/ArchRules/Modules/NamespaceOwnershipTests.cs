using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class NamespaceOwnershipTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Module_assembly_holds_only_the_module_namespace_outside_its_contracts(String name) =>
        Check(
            NamespaceOwnershipRules.ModuleAssemblyHoldsOnlyTheModuleNamespaceOutsideItsContracts(GetModule(name)),
            $"{name} has no project assembly of its own",
            "Move the type under the module's root namespace");

    [Theory]
    [EveryModule]
    public void Contracts_assembly_holds_only_the_contracts_namespace(String name) =>
        Check(
            NamespaceOwnershipRules.ContractsAssemblyHoldsOnlyTheContractsNamespace(GetModule(name)),
            $"{name} has no contracts assembly of its own",
            "Move the type under the module's Contracts namespace");
}
