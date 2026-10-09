using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public static class CoreRules
{
    public static RuleEvaluation CoreProjectsReferenceOnlyCoreAndPackageProjects(ArchSolution solution)
    {
        var coreProjectPaths = solution.GetProjectPaths(solution.CoreFolder);
        IReadOnlyList<String> subject = [.. coreProjectPaths.Select(solution.ToSolutionPath)];

        return RuleEvaluation.Create(
            subject,
            () => coreProjectPaths.SelectMany(projectPath => GetReferencesOutsideTheCore(solution, projectPath)));
    }

    private static IEnumerable<String> GetReferencesOutsideTheCore(ArchSolution solution, String projectPath) =>
        ProjectReferences
            .ReadFullPaths(projectPath)
            .Where(referencePath => !IsInCoreOrPackages(solution, referencePath))
            .Select(referencePath => DescribeReference(solution, projectPath, referencePath));

    private static Boolean IsInCoreOrPackages(ArchSolution solution, String path) =>
        IsInFolder(solution.CoreFolder, path) || IsInFolder(solution.PackagesFolder, path);

    private static Boolean IsInFolder(String folder, String path) =>
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static String DescribeReference(ArchSolution solution, String projectPath, String referencePath) =>
        $"{solution.ToSolutionPath(projectPath)} references {solution.ToSolutionPath(referencePath)}";
}
