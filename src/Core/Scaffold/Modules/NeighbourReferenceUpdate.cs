using Tsikhanau.Monolith.Scaffold.Edits;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class NeighbourReferenceUpdate(ScaffoldedModule module, IFileSystem fileSystem)
{
    private static readonly IReadOnlyList<FileEdit> NoEdits = [];

    public Result<IReadOnlyList<FileEdit>> Plan(Optional<String> neighbour) =>
        neighbour.Match(PlanReference, () => Result.Success(NoEdits));

    private Result<IReadOnlyList<FileEdit>> PlanReference(String neighbour) =>
        ModuleGraph
            .Read(fileSystem, module.App)
            .Bind(graph => graph.References(module.Name, neighbour)
                ? Result.Success(NoEdits)
                : EnsureNoCycle(graph, neighbour)
                    .Bind(_ => AddReference(neighbour)));

    private Result<Unit> EnsureNoCycle(ModuleGraph graph, String neighbour) =>
        graph
            .FindPath(neighbour, module.Name)
            .Match(path => BuildCycleError(neighbour, path), () => Result.Success());

    private Result<Unit> BuildCycleError(String neighbour, IReadOnlyList<String> path) =>
        Error.Failure(
            "scaffold.module",
            $"{module.Name} would reference the Contracts of {neighbour}, which already reaches {module.Name} "
            + $"({String.Join(" -> ", path)}); modules must not reference each other in a cycle");

    private Result<IReadOnlyList<FileEdit>> AddReference(String neighbour) =>
        fileSystem
            .ReadTextFile(module.ProjectPath)
            .Bind(moduleProject => ModuleProjectEdit.AddNeighbourContracts(moduleProject, module, neighbour))
            .Map(IReadOnlyList<FileEdit> (edit) => [edit]);
}
