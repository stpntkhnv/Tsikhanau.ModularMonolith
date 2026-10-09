using System.Xml.Linq;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ProjectReferences
{
    public static IReadOnlyList<String> Read(String projectPath) =>
        ReadIncludePaths(projectPath)
            .Select(GetProjectName)
            .ToList();

    public static IReadOnlyList<String> ReadFullPaths(String projectPath) =>
        ReadIncludePaths(projectPath)
            .Select(includePath => GetFullPath(projectPath, includePath))
            .ToList();

    private static IEnumerable<String> ReadIncludePaths(String projectPath) =>
        XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Attributes("Include")
            .Select(include => include.Value.Replace('\\', '/'));

    private static String GetProjectName(String includePath) => Path.GetFileNameWithoutExtension(includePath);

    private static String GetFullPath(String projectPath, String includePath) =>
        Path.GetFullPath(includePath, Path.GetDirectoryName(projectPath)!);
}
