namespace Tsikhanau.Monolith.ArchRules.Inputs;

public sealed class ArchSolution
{
    public ArchSolution(String root, String rootNamespace)
    {
        Root = root;
        RootNamespace = rootNamespace;
    }

    public String Root { get; }

    public String RootNamespace { get; }

    internal String CoreFolder => Path.Combine(Root, "src", "Core");

    internal String PackagesFolder => Path.Combine(Root, "src", "Packages");

    internal String AspireFolder => Path.Combine(Root, "src", "Aspire");

    internal String ModulesFolder => Path.Combine(Root, "src", "Modules");

    internal IReadOnlyList<String> PackageVersionPaths =>
    [
        Path.Combine(Root, "Directory.Packages.props"),
        Path.Combine(CoreFolder, "Core.Packages.props"),
        Path.Combine(Root, "App.Packages.props"),
    ];

    internal String ModuleProjectPrefix => $"{RootNamespace}.Modules.";

    internal String GetModuleFolder(ArchModule module) => Path.Combine(ModulesFolder, module.Name);

    internal String GetProjectPath(ArchModule module, String projectName) =>
        Path.Combine(GetModuleFolder(module), projectName, $"{projectName}.csproj");

    internal IReadOnlyList<String> GetProjectPaths(String folder) =>
        Directory.Exists(folder) ? [.. GetProjectPathsIn(folder)] : [];

    internal String ToSolutionPath(String path) =>
        Path
            .GetRelativePath(Root, path)
            .Replace('\\', '/');

    private static IEnumerable<String> GetProjectPathsIn(String folder) =>
        Directory
            .GetFiles(folder, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal);
}
