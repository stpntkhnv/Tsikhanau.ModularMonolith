using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class NamespaceOwnershipRules
{
    public static RuleEvaluation ModuleAssemblyHoldsOnlyTheModuleNamespaceOutsideItsContracts(ArchModule module) =>
        module.ProjectAssemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .CollectViolations(type => !module.ContainsOutside(type, "Contracts"));

    public static RuleEvaluation ContractsAssemblyHoldsOnlyTheContractsNamespace(ArchModule module) =>
        module.ContractsAssemblies
            .SelectMany(assembly => assembly.HandwrittenTypes)
            .CollectViolations(type => !module.Contains(type, "Contracts"));
}
