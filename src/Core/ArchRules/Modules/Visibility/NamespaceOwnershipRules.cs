using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules.Visibility;

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
