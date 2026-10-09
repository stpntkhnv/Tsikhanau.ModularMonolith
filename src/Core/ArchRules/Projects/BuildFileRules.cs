using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class BuildFileRules
{
    private static readonly String[] BuildFileNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Build.rsp",
        "Directory.Packages.props",
        ".editorconfig",
        ".globalconfig",
    ];

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
            .. FindBuildFiles(new DirectoryInfo(solution.Root))
                .Select(file => solution.ToSolutionPath(file.FullName))
                .Order(StringComparer.Ordinal),
        ];

        return RuleEvaluation.Create(buildFiles, () => buildFiles.Where(IsOutsideTheCore));
    }

    private static IEnumerable<FileInfo> FindBuildFiles(DirectoryInfo folder) =>
        folder
            .EnumerateFiles()
            .Where(IsBuildFile)
            .Concat(
                folder
                    .EnumerateDirectories()
                    .Where(IsSearched)
                    .SelectMany(FindBuildFiles));

    private static Boolean IsBuildFile(FileInfo file) =>
        BuildFileNames.Contains(file.Name, StringComparer.OrdinalIgnoreCase);

    private static Boolean IsSearched(DirectoryInfo folder) =>
        folder is { LinkTarget: null, Name: not ".git" } && !IsRepositoryOfItsOwn(folder) && !IsBuildOutput(folder);

    private static Boolean IsRepositoryOfItsOwn(DirectoryInfo folder) =>
        Path.Exists(Path.Combine(folder.FullName, ".git"));

    private static Boolean IsBuildOutput(DirectoryInfo folder) =>
        folder is { Name: "bin" or "obj", Parent: { } project } && HoldsAProject(project);

    private static Boolean HoldsAProject(DirectoryInfo folder) =>
        folder
            .EnumerateFiles("*.csproj")
            .Any();

    private static Boolean IsOutsideTheCore(String path) => !CoreRootFiles.Contains(path) && !IsInACoreFolder(path);

    private static Boolean IsInACoreFolder(String path) =>
        CoreFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));
}
