using Xunit;

namespace Tsikhanau.Monolith.Testing.Modules;

public static class ProductionModules
{
    public const String Placeholder = "(none)";

    public static readonly String[] Names = ReadNames(SolutionPaths.Modules);

    public static TheoryData<String> All => CreateRows(Names);

    public static TheoryData<String, String> Pairs => CreatePairRows(Names);

    internal static TheoryData<String> CreateRows(IReadOnlyList<String> names) =>
        names.Count == 0 ? [Placeholder] : [.. names];

    internal static TheoryData<String, String> CreatePairRows(IReadOnlyList<String> names)
    {
        (String Name, String OtherName)[] orderedPairs = [.. names.SelectMany(name => PairWithOthers(name, names))];

        return orderedPairs.Length == 0 ? [(Placeholder, Placeholder)] : [.. orderedPairs];
    }

    internal static String[] ReadNames(String modulesFolder)
    {
        var modulesDirectory = new DirectoryInfo(modulesFolder);

        return modulesDirectory.Exists ? [.. ReadProjectFolderNames(modulesDirectory)] : [];
    }

    private static IEnumerable<String> ReadProjectFolderNames(DirectoryInfo modulesDirectory) =>
        modulesDirectory
            .EnumerateDirectories()
            .Where(ContainsProject)
            .Select(directory => directory.Name)
            .Order();

    private static Boolean ContainsProject(DirectoryInfo directory) =>
        directory
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Any();

    private static IEnumerable<(String Name, String OtherName)> PairWithOthers(
        String name,
        IReadOnlyList<String> names) =>
        names
            .Where(otherName => otherName != name)
            .Select(otherName => (name, otherName));
}
