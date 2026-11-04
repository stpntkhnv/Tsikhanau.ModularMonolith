using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class ModuleReader(IFileSystem fileSystem)
{
    public Result<ScaffoldedModule> Read(AppLayout app, String name)
    {
        var moduleNames = ModuleFolders.ReadNames(fileSystem);

        return moduleNames.Contains(name, StringComparer.Ordinal)
            ? ScaffoldedModule
                .Parse(app, name)
                .Bind(EnsureProjects)
            : Error.Failure(
                "scaffold.module",
                $"{ModuleFolders.Root} has no module {name} (modules: {BuildNameList(moduleNames)})");
    }

    private Result<ScaffoldedModule> EnsureProjects(ScaffoldedModule module) =>
        FindMissingProject(module.Projects)
            .Match(
                projectPath => Error.Failure(
                    "scaffold.module",
                    $"{module.Projects.Folder} holds no project {projectPath}"),
                () => Result.Success(module));

    private Optional<String> FindMissingProject(ModuleProjects projects) =>
        projects.ProjectPaths.FirstOrDefault(projectPath => !fileSystem.FileExists(projectPath));

    private static String BuildNameList(IReadOnlyList<String> names) =>
        names.Count == 0 ? "none" : String.Join(", ", names);
}
