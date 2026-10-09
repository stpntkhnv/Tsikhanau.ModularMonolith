using System.Xml.Linq;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class ProjectReferences
{
    public static IReadOnlyList<String> Read(String projectPath) =>
        XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Select(GetProjectName)
            .ToList();

    public static IReadOnlyList<String> ReadFullPaths(String projectPath) =>
        XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => GetFullPath(projectPath, reference))
            .ToList();

    private static String GetProjectName(XElement reference) =>
        Path.GetFileNameWithoutExtension(ReadIncludePath(reference));

    private static String GetFullPath(String projectPath, XElement reference) =>
        Path.GetFullPath(ReadIncludePath(reference), Path.GetDirectoryName(projectPath)!);

    private static String ReadIncludePath(XElement reference) =>
        reference
            .Attribute("Include")!.Value
            .Replace('\\', '/');
}
