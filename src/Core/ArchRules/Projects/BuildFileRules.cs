using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class BuildFileRules
{
    private static readonly String[] ProjectBuildFileNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Build.rsp",
        "Directory.Packages.props",
    ];

    private static readonly String[] SourceConfigFileNames = [".editorconfig", ".globalconfig"];

    private static readonly String[] CoreRootFiles =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        ".editorconfig",
    ];

    private static readonly String[] CoreFolders = ["src/Core/", "src/Packages/"];

    public static RuleEvaluation OnlyTheCoreHasBuildAndAnalyzerConfigFiles(ArchSolution solution)
    {
        IReadOnlyList<String> buildFiles =
        [
            .. GetFoldersThatAffectCompilation(solution)
                .SelectMany(FindBuildFiles)
                .Select(solution.ToSolutionPath)
                .Order(StringComparer.Ordinal),
        ];

        return RuleEvaluation.Create(buildFiles, () => buildFiles.Where(IsOutsideTheCore));
    }

    internal static IEnumerable<CheckedFolder> GetFoldersThatAffectCompilation(ArchSolution solution)
    {
        var root = Path.TrimEndingDirectorySeparator(solution.Root);

        return SolutionProjects
            .ReadWithTheirReferences(solution.SolutionFilePaths)
            .Select(projectPath => new DirectoryInfo(Path.GetDirectoryName(projectPath)!))
            .SelectMany(projectFolder => GetFoldersOfTheProject(root, projectFolder))
            .Prepend(new CheckedFolder(root, ReadsSourceConfigFiles: true))
            .GroupBy(folder => folder.Path, StringComparer.Ordinal)
            .Select(MergeFolder);
    }

    private static IEnumerable<CheckedFolder> GetFoldersOfTheProject(String root, DirectoryInfo projectFolder) =>
    [
        .. GetFoldersAbove(root, projectFolder),
        new CheckedFolder(projectFolder.FullName, ReadsSourceConfigFiles: true),
        .. GetFoldersBelow(projectFolder),
    ];

    private static IEnumerable<CheckedFolder> GetFoldersAbove(String root, DirectoryInfo folder) =>
        folder.Parent is { } parent && IsInside(root, parent.FullName)
            ? [new CheckedFolder(parent.FullName, ReadsSourceConfigFiles: true), .. GetFoldersAbove(root, parent)]
            : [];

    private static IReadOnlyList<CheckedFolder> GetFoldersBelow(DirectoryInfo folder) =>
    [
        .. folder
            .EnumerateDirectories()
            .Where(MayHoldCompiledFiles)
            .SelectMany(GetFolderAndFoldersBelow),
    ];

    private static IReadOnlyList<CheckedFolder> GetFolderAndFoldersBelow(DirectoryInfo folder)
    {
        var foldersBelow = GetFoldersBelow(folder);
        var holdsSourceFiles = HasSourceFiles(folder) || foldersBelow.Any(below => below.ReadsSourceConfigFiles);

        return [new CheckedFolder(folder.FullName, holdsSourceFiles), .. foldersBelow];
    }

    private static CheckedFolder MergeFolder(IGrouping<String, CheckedFolder> folders) =>
        new(folders.Key, folders.Any(folder => folder.ReadsSourceConfigFiles));

    private static Boolean HasSourceFiles(DirectoryInfo folder) =>
        folder
            .EnumerateFiles("*.cs")
            .Any();

    private static Boolean IsInside(String root, String folder) =>
        folder == root || folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static Boolean MayHoldCompiledFiles(DirectoryInfo folder) =>
        folder is { LinkTarget: null, Name: not "node_modules" }
        && !folder.Name.StartsWith('.')
        && !IsBuildOutput(folder);

    private static Boolean IsBuildOutput(DirectoryInfo folder) =>
        folder is { Name: "bin" or "obj", Parent: { } project } && HoldsAProject(project);

    private static Boolean HoldsAProject(DirectoryInfo folder) =>
        folder
            .EnumerateFiles("*.csproj")
            .Any();

    private static IEnumerable<String> FindBuildFiles(CheckedFolder folder) =>
        Directory
            .EnumerateFiles(folder.Path)
            .Where(path => IsProjectBuildFile(path) || (folder.ReadsSourceConfigFiles && IsSourceConfigFile(path)));

    private static Boolean IsProjectBuildFile(String path) => IsNamedOneOf(path, ProjectBuildFileNames);

    private static Boolean IsSourceConfigFile(String path) => IsNamedOneOf(path, SourceConfigFileNames);

    private static Boolean IsNamedOneOf(String path, String[] names) =>
        names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private static Boolean IsOutsideTheCore(String path) => !CoreRootFiles.Contains(path) && !IsInACoreFolder(path);

    private static Boolean IsInACoreFolder(String path) =>
        CoreFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));

    internal sealed record CheckedFolder(String Path, Boolean ReadsSourceConfigFiles);
}
