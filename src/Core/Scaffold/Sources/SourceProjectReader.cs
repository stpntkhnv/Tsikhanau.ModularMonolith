using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Sources;

internal sealed class SourceProjectReader(IFileSystem fileSystem)
{
    private const String SourcePattern = "*.cs";

    private static readonly String[] BuildFolders = ["bin", "obj"];

    public Result<IReadOnlyList<SourceProject>> Read(AppLayout app) =>
        ModuleFolders
            .ReadNames(fileSystem)
            .Order(StringComparer.Ordinal)
            .SelectMany(moduleName => ReadProjects(new ModuleProjects(app.ProjectPrefix, moduleName)))
            .CombineAll();

    public Result<IReadOnlyList<SourceProject>> Read(AppLayout app, String moduleName) =>
        ReadProjects(new ModuleProjects(app.ProjectPrefix, moduleName))
            .CombineAll();

    private IEnumerable<Result<SourceProject>> ReadProjects(ModuleProjects projects) =>
        [
            ReadProject(projects.ModuleName, projects.ProjectFolder, isContracts: false),
            ReadProject(projects.ModuleName, projects.ContractsFolder, isContracts: true),
        ];

    private Result<SourceProject> ReadProject(String moduleName, String projectFolder, Boolean isContracts) =>
        GetSourceFiles(projectFolder)
            .Select(ReadSourceFile)
            .CombineAll()
            .Map(files => new SourceProject(moduleName, isContracts, files));

    private IEnumerable<String> GetSourceFiles(String folder) =>
        fileSystem
            .GetFiles(folder, SourcePattern)
            .Concat(fileSystem
                .GetDirectories(folder, "*")
                .Where(IsSourceFolder)
                .SelectMany(GetSourceFiles));

    private Result<SourceFile> ReadSourceFile(String path) =>
        fileSystem
            .ReadTextFile(path)
            .Map(file => new SourceFile(path, file.Text));

    private static Boolean IsSourceFolder(String folder) => !BuildFolders.Contains(Path.GetFileName(folder));
}
