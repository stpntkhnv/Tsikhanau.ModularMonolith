using System.Xml.Linq;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class SolutionProjects
{
    public static IReadOnlyList<String> ReadWithTheirReferences(IEnumerable<String> solutionPaths)
    {
        var projectPaths = new HashSet<String>(StringComparer.Ordinal);

        solutionPaths
            .SelectMany(ReadProjectPaths)
            .ForEach(projectPath => AddWithItsReferences(projectPaths, projectPath));

        return [.. projectPaths];
    }

    private static IEnumerable<String> ReadProjectPaths(String solutionPath) =>
        XDocument
            .Load(solutionPath)
            .Descendants("Project")
            .Attributes("Path")
            .Select(path => ToFullPath(solutionPath, path.Value));

    private static String ToFullPath(String solutionPath, String projectPath) =>
        Path.GetFullPath(projectPath.Replace('\\', '/'), Path.GetDirectoryName(solutionPath)!);

    private static void AddWithItsReferences(HashSet<String> projectPaths, String projectPath)
    {
        if (File.Exists(projectPath) && projectPaths.Add(projectPath))
        {
            ProjectReferences
                .ReadFullPaths(projectPath)
                .ForEach(referencePath => AddWithItsReferences(projectPaths, referencePath));
        }
    }
}
