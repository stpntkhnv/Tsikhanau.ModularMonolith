using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.ProjectFiles;
using Tsikhanau.Monolith.Testing.Modules;

namespace Tsikhanau.Monolith.ArchRules.Projects.References;

public static class ModuleGraphRules
{
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
        [.. modules.All.Where(module => File.Exists(solution.GetProjectPath(module)))];

    private static IReadOnlyList<String> GetProjectPaths(ArchSolution solution, IEnumerable<ArchModule> modules) =>
        [.. modules.Select(module => solution.ToSolutionPath(solution.GetProjectPath(module)))];

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
        $"{module.Name} references {BuildContractsName(solution, referencedName)}";

    private static String BuildContractsName(ArchSolution solution, String moduleName) =>
        ModuleLayout.BuildContractsName(ModuleLayout.BuildRootNamespace(solution.RootNamespace, moduleName));

    private static Dictionary<String, IReadOnlyList<String>> ReadEdges(
        ArchSolution solution,
        IEnumerable<ArchModule> modules) =>
        modules.ToDictionary(module => module.Name, module => ReadReferencedModules(solution, module));

    private static IReadOnlyList<String> ReadReferencedModules(ArchSolution solution, ArchModule module) =>
        ProjectReferences
            .Read(solution.GetProjectPath(module))
            .Where(projectName => ModuleLayout.IsContractsName(solution.RootNamespace, projectName))
            .Select(projectName => ModuleLayout.ParseModuleName(solution.RootNamespace, projectName))
            .Where(otherName => otherName != module.Name)
            .ToList();

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
