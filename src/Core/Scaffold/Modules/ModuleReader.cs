using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed class ModuleReader(IFileSystem fileSystem)
{
    public Result<ScaffoldedModule> Read(AppLayout app, String name)
    {
        var moduleNames = ReadModuleNames();

        return moduleNames.Contains(name, StringComparer.Ordinal)
            ? ModuleName
                .Parse(name, app.DatabaseName)
                .Map(moduleName => new ScaffoldedModule(app, moduleName))
                .Bind(EnsureProjects)
            : Error.Failure(
                "scaffold.module",
                $"{ScaffoldedModule.ModulesFolder} has no module {name} (modules: {BuildNameList(moduleNames)})");
    }

    private IReadOnlyList<String> ReadModuleNames() =>
        [
            .. fileSystem
                .GetDirectories(ScaffoldedModule.ModulesFolder, "*")
                .Select(folder => Path.GetFileName(folder)),
        ];

    private Result<ScaffoldedModule> EnsureProjects(ScaffoldedModule module) =>
        FindMissingProject(module)
            .Match(
                projectPath => Error.Failure("scaffold.module", $"{module.Folder} holds no project {projectPath}"),
                () => Result.Success(module));

    private Optional<String> FindMissingProject(ScaffoldedModule module) =>
        module.ProjectPaths.FirstOrDefault(projectPath => !fileSystem.FileExists(projectPath));

    private static String BuildNameList(IReadOnlyList<String> names) =>
        names.Count == 0 ? "none" : String.Join(", ", names);
}
