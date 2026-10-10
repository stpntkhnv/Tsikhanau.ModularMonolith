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
                .Parse(name, app.LoginPrefix)
                .Map(moduleName => new ScaffoldedModule(app, moduleName))
                .Ensure(HasProject, BuildMissingProjectError)
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

    private Boolean HasProject(ScaffoldedModule module) => fileSystem.FileExists(module.ProjectPath);

    private static Error BuildMissingProjectError(ScaffoldedModule module) =>
        Error.Failure("scaffold.module", $"{module.Folder} holds no project {module.ProjectPath}");

    private static String BuildNameList(IReadOnlyList<String> names) =>
        names.Count == 0 ? "none" : String.Join(", ", names);
}
