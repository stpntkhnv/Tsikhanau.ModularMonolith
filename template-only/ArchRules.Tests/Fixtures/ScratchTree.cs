using System.Xml.Linq;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;

namespace Tsikhanau.Monolith.ArchRules.Tests.Fixtures;

internal sealed class ScratchTree : IDisposable
{
    public const String GoodProject = "src/Modules/Good/Acme.Modules.Good/Acme.Modules.Good.csproj";

    public const String GoodContracts =
        "src/Modules/Good/Acme.Modules.Good.Contracts/Acme.Modules.Good.Contracts.csproj";

    public const String PeerProject = "src/Modules/Peer/Acme.Modules.Peer/Acme.Modules.Peer.csproj";

    public const String PeerContracts =
        "src/Modules/Peer/Acme.Modules.Peer.Contracts/Acme.Modules.Peer.Contracts.csproj";

    public const String CoreDomain = "src/Core/Domain/Tsikhanau.Monolith.Domain.csproj";

    public const String CoreContracts = "src/Core/Contracts/Tsikhanau.Monolith.Contracts.csproj";

    public const String AppPackages = "App.Packages.props";

    public const String AspireVersion = "13.0.0";

    private const String Railway = "src/Packages/Tsikhanau.Railway/Tsikhanau.Railway.csproj";

    private const String AppHost = "src/Aspire/Acme.AppHost/Acme.AppHost.csproj";

    private const String RootPackages = "Directory.Packages.props";

    private const String CorePackages = "src/Core/Core.Packages.props";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("arch-tree-");

    private ScratchTree()
    {
        AddProject(GoodProject, GoodContracts, PeerContracts, CoreDomain);
        AddProject(GoodContracts, CoreContracts);
        AddProject(PeerProject, PeerContracts);
        AddProject(PeerContracts, CoreContracts);
        AddProject(CoreDomain);
        AddProject(CoreContracts, Railway);
        AddProject(Railway);
        AddDocument(AppHost, BuildAppHost());
        AddPackageFile(RootPackages);
        AddPackageFile(CorePackages);
        AddPackageFile(AppPackages);
        AddPackageVersion(RootPackages, "Aspire.Hosting.AppHost", AspireVersion);
        AddFile("Directory.Build.props", "<Project />");
        AddFile("Directory.Build.targets", "<Project />");
        AddFile(".editorconfig");
        AddDocument("Acme.slnx", BuildSolution(GoodProject, GoodContracts, PeerProject, PeerContracts, AppHost));
    }

    private ArchSolution Solution => new(_folder.FullName, FixtureModules.RootNamespace);

    public static RuleEvaluation Evaluate(Func<ArchSolution, RuleEvaluation> rule) => Evaluate(_ => { }, rule);

    public static RuleEvaluation Evaluate(Action<ScratchTree> change, Func<ArchSolution, RuleEvaluation> rule)
    {
        using var tree = new ScratchTree();
        change(tree);

        return rule(tree.Solution);
    }

    public void AddProject(String path, params IEnumerable<String> referencePaths) =>
        AddDocument(path, BuildProject(path, referencePaths));

    public void AddReference(String projectPath, String referencePath) =>
        AddItem(projectPath, BuildProjectReference(projectPath, referencePath));

    public void AddPackageVersion(String path, String name, String version) =>
        AddItem(path, BuildPackageVersion(name, version));

    public void AddFile(String path, String text = "")
    {
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }

    public void Dispose() => _folder.Delete(recursive: true);

    private static XElement BuildProject(String path, IEnumerable<String> referencePaths) =>
        new(
            "Project",
            new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            BuildItemGroup(referencePaths.Select(referencePath => BuildProjectReference(path, referencePath))));

    private static XElement BuildAppHost() =>
        new("Project", new XAttribute("Sdk", $"Aspire.AppHost.Sdk/{AspireVersion}"));

    private static XElement BuildPackageFile() => new("Project", BuildItemGroup([]));

    private static XElement BuildSolution(params IEnumerable<String> projectPaths) =>
        new("Solution", projectPaths.Select(BuildSolutionProject));

    private static XElement BuildItemGroup(IEnumerable<XElement> items) => new("ItemGroup", items);

    private static XElement BuildProjectReference(String projectPath, String referencePath) =>
        new("ProjectReference", new XAttribute("Include", BuildIncludePath(projectPath, referencePath)));

    private static XElement BuildPackageVersion(String name, String version) =>
        new("PackageVersion", new XAttribute("Include", name), new XAttribute("Version", version));

    private static XElement BuildSolutionProject(String projectPath) =>
        new("Project", new XAttribute("Path", projectPath));

    private static String BuildIncludePath(String projectPath, String referencePath) =>
        Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, referencePath);

    private void AddPackageFile(String path) => AddDocument(path, BuildPackageFile());

    private void AddDocument(String path, XElement document) => AddFile(path, document.ToString());

    private void AddItem(String path, XElement item)
    {
        var fullPath = GetFullPath(path);
        var document = XDocument.Load(fullPath);

        document
            .Descendants("ItemGroup")
            .First()
            .Add(item);

        document.Save(fullPath);
    }

    private String GetFullPath(String path) => Path.GetFullPath(path, _folder.FullName);
}
