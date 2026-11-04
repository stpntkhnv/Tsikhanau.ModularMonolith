using Tsikhanau.Monolith.Testing.Modules;

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

    internal String ModulesFolder => ModuleLayout.GetModulesFolder(Root);

    internal IReadOnlyList<String> PackageVersionPaths =>
    [
        Path.Combine(Root, "Directory.Packages.props"),
        Path.Combine(CoreFolder, "Core.Packages.props"),
        Path.Combine(Root, "App.Packages.props"),
    ];

    internal IEnumerable<String> SolutionFilePaths =>
        Directory
            .EnumerateFiles(Root, "*.slnx")
            .Append(Path.Combine(Root, "template-only", "Template.slnx"))
            .Where(File.Exists);

    internal String GetModuleFolder(ArchModule module) => Path.Combine(ModulesFolder, module.Name);

    internal String GetProjectPath(ArchModule module) => GetProjectPath(module, module.RootNamespace);

    internal String GetContractsProjectPath(ArchModule module) => GetProjectPath(module, module.ContractsName);

    internal IEnumerable<String> GetModuleProjectPaths(ArchModule module) =>
        module.ProjectNames.Select(projectName => GetProjectPath(module, projectName));

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

    private String GetProjectPath(ArchModule module, String projectName) =>
        Path.Combine(GetModuleFolder(module), projectName, $"{projectName}.csproj");
}
