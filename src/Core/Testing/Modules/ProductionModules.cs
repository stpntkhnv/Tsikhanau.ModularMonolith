using Xunit;

namespace Tsikhanau.Monolith.Testing.Modules;

public static class ProductionModules
{
    public const String Placeholder = "(none)";

    public static readonly String[] Names =
    [
        .. new DirectoryInfo(SolutionPaths.Modules)
            .EnumerateDirectories()
            .Where(ContainsProject)
            .Select(directory => directory.Name)
            .Order(),
    ];

    private static readonly (String Name, String OtherName)[] OrderedPairs = [.. Names.SelectMany(PairWithOthers)];

    public static TheoryData<String> All => Names.Length == 0 ? [Placeholder] : [.. Names];

    public static TheoryData<String, String> Pairs =>
        OrderedPairs.Length == 0 ? [(Placeholder, Placeholder)] : [.. OrderedPairs];

    private static Boolean ContainsProject(DirectoryInfo directory) =>
        directory
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Any();

    private static IEnumerable<(String Name, String OtherName)> PairWithOthers(String name) =>
        Names
            .Where(otherName => otherName != name)
            .Select(otherName => (name, otherName));
}
