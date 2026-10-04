using Xunit;

namespace ModularMonolith.Testing.Modules;

public static class ProductionModules
{
    public static readonly String[] Names =
    [
        .. new DirectoryInfo(SolutionPaths.Modules)
            .EnumerateDirectories()
            .Where(ContainsProject)
            .Select(directory => directory.Name)
            .Order(),
    ];

    public static TheoryData<String> All => [.. Names];

    private static Boolean ContainsProject(DirectoryInfo directory) =>
        directory
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Any();
}
