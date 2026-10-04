using System.Xml.Linq;

namespace ModularMonolith.ArchTests.Support;

public static class ProjectReferences
{
    public static IReadOnlyList<String> Read(String projectPath) =>
        XDocument
            .Load(projectPath)
            .Descendants("ProjectReference")
            .Select(GetProjectName)
            .ToList();

    private static String GetProjectName(XElement reference)
    {
        var includePath = reference.Attribute("Include")!.Value;
        return Path.GetFileNameWithoutExtension(includePath.Replace('\\', '/'));
    }
}
