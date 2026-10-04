using ModularMonolith.ArchTests.Support;
using ModularMonolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class ModuleGraphTests
{
    private const String ContractsPrefix = "ModularMonolith.Modules.";
    private const String ContractsSuffix = ".Contracts";

    [Fact]
    public void Modules_do_not_reference_each_other_in_a_cycle()
    {
        Assert.SkipWhen(ProductionModules.Names.Length == 0, "There are no production modules");

        var edges = ProductionModules.Names.ToDictionary(name => name, ReadReferencedModules);

        var cycles = FindCycles(edges);

        cycles.ShouldBeEmpty();
    }

    [Fact]
    public void Modules_reference_only_contracts_of_known_modules()
    {
        Assert.SkipWhen(ProductionModules.Names.Length == 0, "There are no production modules");

        var unknownReferences = ProductionModules.Names
            .SelectMany(FindUnknownReferences)
            .ToList();

        unknownReferences.ShouldBeEmpty();
    }

    private static IEnumerable<String> FindUnknownReferences(String name) =>
        ReadReferencedModules(name)
            .Where(referencedName => !ProductionModules.Names.Contains(referencedName))
            .Select(referencedName => $"{name} references {ContractsPrefix}{referencedName}{ContractsSuffix}");

    private static IReadOnlyList<String> ReadReferencedModules(String name) =>
        ProjectReferences
            .Read(
                Path.Combine(
                    SolutionPaths.Modules,
                    name,
                    $"ModularMonolith.Modules.{name}",
                    $"ModularMonolith.Modules.{name}.csproj"))
            .Where(IsContractsProject)
            .Select(ReadModuleOfContracts)
            .Where(otherName => otherName != name)
            .ToList();

    private static Boolean IsContractsProject(String projectName) =>
        projectName.StartsWith(ContractsPrefix, StringComparison.Ordinal)
        && projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    private static String ReadModuleOfContracts(String projectName) =>
        projectName[ContractsPrefix.Length..^ContractsSuffix.Length];

    private static IReadOnlyList<String> FindCycles(IReadOnlyDictionary<String, IReadOnlyList<String>> edges)
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
