using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Projects.BuildSettings;

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
            .. CompiledFolders
                .Read(solution)
                .SelectMany(FindBuildFiles)
                .Select(solution.ToSolutionPath)
                .Order(StringComparer.Ordinal),
        ];

        return RuleEvaluation.Create(buildFiles, () => buildFiles.Where(IsOutsideTheCore));
    }

    private static IEnumerable<String> FindBuildFiles(CompiledFolder folder) =>
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
}
