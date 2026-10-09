using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class ModuleGraphRules
{
    private const String ContractsSuffix = ".Contracts";

    public static RuleEvaluation ModulesDoNotReferenceEachOtherInACycle(ArchSolution solution, ArchModules modules)
    {
        var modulesWithAProject = GetModulesWithAProject(solution, modules);

        return RuleEvaluation.Create(
            GetProjectPaths(solution, modulesWithAProject),
            () => GetCycles(ReadEdges(solution, modulesWithAProject)));
    }

    public static RuleEvaluation ModulesReferenceOnlyContractsOfKnownModules(
        ArchSolution solution,
        ArchModules modules)
    {
        var modulesWithAProject = GetModulesWithAProject(solution, modules);

        return RuleEvaluation.Create(
            GetProjectPaths(solution, modulesWithAProject),
            () => modulesWithAProject.SelectMany(module => GetUnknownReferences(solution, modules, module)));
    }

    private static IReadOnlyList<ArchModule> GetModulesWithAProject(ArchSolution solution, ArchModules modules) =>
        [.. modules.All.Where(module => File.Exists(GetProjectPath(solution, module)))];

    private static IReadOnlyList<String> GetProjectPaths(ArchSolution solution, IEnumerable<ArchModule> modules) =>
        [.. modules.Select(module => solution.ToSolutionPath(GetProjectPath(solution, module)))];

    private static String GetProjectPath(ArchSolution solution, ArchModule module) =>
        solution.GetProjectPath(module, module.RootNamespace);

    private static IEnumerable<String> GetUnknownReferences(
        ArchSolution solution,
        ArchModules modules,
        ArchModule module) =>
        ReadReferencedModules(solution, module)
            .Where(referencedName => !IsKnown(modules, referencedName))
            .Select(referencedName => DescribeReference(solution, module, referencedName));

    private static Boolean IsKnown(ArchModules modules, String name) =>
        modules.All.Any(module => module.Name == name);

    private static String DescribeReference(ArchSolution solution, ArchModule module, String referencedName) =>
        $"{module.Name} references {solution.ModuleProjectPrefix}{referencedName}{ContractsSuffix}";

    private static Dictionary<String, IReadOnlyList<String>> ReadEdges(
        ArchSolution solution,
        IEnumerable<ArchModule> modules) =>
        modules.ToDictionary(module => module.Name, module => ReadReferencedModules(solution, module));

    private static IReadOnlyList<String> ReadReferencedModules(ArchSolution solution, ArchModule module) =>
        ProjectReferences
            .Read(GetProjectPath(solution, module))
            .Where(projectName => IsModuleContracts(solution, projectName))
            .Select(projectName => GetModuleOfContracts(solution, projectName))
            .Where(otherName => otherName != module.Name)
            .ToList();

    private static Boolean IsModuleContracts(ArchSolution solution, String projectName) =>
        projectName.StartsWith(solution.ModuleProjectPrefix, StringComparison.Ordinal)
        && projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    private static String GetModuleOfContracts(ArchSolution solution, String projectName) =>
        projectName[solution.ModuleProjectPrefix.Length..^ContractsSuffix.Length];

    private static IReadOnlyList<String> GetCycles(IReadOnlyDictionary<String, IReadOnlyList<String>> edges)
    {
        var states = edges.Keys.ToDictionary(name => name, _ => VisitState.NotVisited);
        var cycles = new List<String>();

        foreach (var name in edges.Keys)
        {
            Visit(name, [], edges, states, cycles);
        }

        return cycles;
    }

    private static void Visit(
        String name,
        List<String> path,
        IReadOnlyDictionary<String, IReadOnlyList<String>> edges,
        Dictionary<String, VisitState> states,
        List<String> cycles)
    {
        switch (states[name])
        {
            case VisitState.InStack:
                cycles.Add(FormatCycle(path, name));
                return;
            case VisitState.Done:
                return;
        }

        states[name] = VisitState.InStack;
        path.Add(name);

        foreach (var referencedName in edges[name].Where(edges.ContainsKey))
        {
            Visit(referencedName, path, edges, states, cycles);
        }

        path.RemoveAt(path.Count - 1);
        states[name] = VisitState.Done;
    }

    private static String FormatCycle(List<String> path, String repeatedName)
    {
        var cycleStart = path.IndexOf(repeatedName);
        var cycleNames = path[cycleStart..];

        return String.Join(" -> ", [.. cycleNames, repeatedName]);
    }

    private enum VisitState
    {
        NotVisited,
        InStack,
        Done,
    }
}
