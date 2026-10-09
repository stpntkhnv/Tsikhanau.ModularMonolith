using ModularMonolith.ArchTests.Support;
using ModularMonolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class CoreTests
{
    private static readonly String CoreFolder = Path.Combine(SolutionPaths.Root, "src", "Core");

    private static readonly String PackagesFolder = Path.Combine(SolutionPaths.Root, "src", "Packages");

    [Fact]
    public void Core_projects_reference_only_core_and_package_projects()
    {
        var coreProjectPaths = Directory.GetFiles(CoreFolder, "*.csproj", SearchOption.AllDirectories);

        var referencesOutsideTheCore = coreProjectPaths
            .SelectMany(FindReferencesOutsideTheCore)
            .ToList();

        coreProjectPaths.ShouldNotBeEmpty();
        referencesOutsideTheCore.ShouldBeEmpty();
    }

    private static IEnumerable<String> FindReferencesOutsideTheCore(String projectPath) =>
        ProjectReferences
            .ReadFullPaths(projectPath)
            .Where(referencePath => !IsInCoreOrPackages(referencePath))
            .Select(referencePath => $"{ToSolutionPath(projectPath)} references {ToSolutionPath(referencePath)}");

    private static Boolean IsInCoreOrPackages(String path) =>
        path.StartsWith(CoreFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || path.StartsWith(PackagesFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static String ToSolutionPath(String path) =>
        Path
            .GetRelativePath(SolutionPaths.Root, path)
            .Replace('\\', '/');
}
