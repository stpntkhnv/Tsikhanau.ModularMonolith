using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.ProjectFiles;

namespace Tsikhanau.Monolith.ArchRules.Projects.BuildSettings;

internal static class CompiledFolders
{
    public static IReadOnlyList<CompiledFolder> Read(ArchSolution solution)
    {
        var root = Path.TrimEndingDirectorySeparator(solution.Root);

        return
        [
            .. SolutionProjects
                .ReadWithTheirReferences(solution.SolutionFilePaths)
                .Select(projectPath => new DirectoryInfo(Path.GetDirectoryName(projectPath)!))
                .SelectMany(projectFolder => GetFoldersOfTheProject(root, projectFolder))
                .Prepend(new CompiledFolder(root, ReadsSourceConfigFiles: true))
                .GroupBy(folder => folder.Path, StringComparer.Ordinal)
                .Select(MergeFolder),
        ];
    }

    private static IEnumerable<CompiledFolder> GetFoldersOfTheProject(String root, DirectoryInfo projectFolder) =>
    [
        .. GetFoldersAbove(root, projectFolder),
        new CompiledFolder(projectFolder.FullName, ReadsSourceConfigFiles: true),
        .. GetFoldersBelow(projectFolder),
    ];

    private static IEnumerable<CompiledFolder> GetFoldersAbove(String root, DirectoryInfo folder) =>
        folder.Parent is { } parent && IsInside(root, parent.FullName)
            ? [new CompiledFolder(parent.FullName, ReadsSourceConfigFiles: true), .. GetFoldersAbove(root, parent)]
            : [];

    private static IReadOnlyList<CompiledFolder> GetFoldersBelow(DirectoryInfo folder) =>
    [
        .. folder
            .EnumerateDirectories()
            .Where(MayHoldCompiledFiles)
            .SelectMany(GetFolderAndFoldersBelow),
    ];

    private static IReadOnlyList<CompiledFolder> GetFolderAndFoldersBelow(DirectoryInfo folder)
    {
        var foldersBelow = GetFoldersBelow(folder);
        var holdsSourceFiles = HasSourceFiles(folder) || foldersBelow.Any(below => below.ReadsSourceConfigFiles);

        return [new CompiledFolder(folder.FullName, holdsSourceFiles), .. foldersBelow];
    }

    private static CompiledFolder MergeFolder(IGrouping<String, CompiledFolder> folders) =>
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
}
