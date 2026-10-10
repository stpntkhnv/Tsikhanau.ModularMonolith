using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed class SourceProjectReader(IFileSystem fileSystem)
{
    private const String SourcePattern = "*.cs";

    private static readonly String[] BuildFolders = ["bin", "obj"];

    public Result<IReadOnlyList<SourceProject>> Read(AppLayout app) =>
        fileSystem
            .GetDirectories(ScaffoldedModule.ModulesFolder, "*")
            .Select(Path.GetFileName)
            .OfType<String>()
            .Order(StringComparer.Ordinal)
            .SelectMany(moduleName => ReadProjects(app, moduleName))
            .CombineAll();

    public Result<IReadOnlyList<SourceProject>> Read(AppLayout app, String moduleName) =>
        ReadProjects(app, moduleName)
            .CombineAll();

    private IEnumerable<Result<SourceProject>> ReadProjects(AppLayout app, String moduleName)
    {
        var projectName = $"{app.ProjectPrefix}.Modules.{moduleName}";

        return
        [
            ReadProject(moduleName, projectName, isContracts: false),
            ReadProject(moduleName, $"{projectName}.Contracts", isContracts: true),
        ];
    }

    private Result<SourceProject> ReadProject(String moduleName, String projectName, Boolean isContracts) =>
        FindSourceFiles($"{ScaffoldedModule.ModulesFolder}/{moduleName}/{projectName}")
            .Select(ReadSourceFile)
            .CombineAll()
            .Map(files => new SourceProject(moduleName, isContracts, files));

    private IEnumerable<String> FindSourceFiles(String folder) =>
        fileSystem
            .GetFiles(folder, SourcePattern)
            .Concat(fileSystem
                .GetDirectories(folder, "*")
                .Where(IsSourceFolder)
                .SelectMany(FindSourceFiles));

    private Result<SourceFile> ReadSourceFile(String path) =>
        fileSystem
            .ReadTextFile(path)
            .Map(file => new SourceFile(path, file.Text));

    private static Boolean IsSourceFolder(String folder) => !BuildFolders.Contains(Path.GetFileName(folder));
}
