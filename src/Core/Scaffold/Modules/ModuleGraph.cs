using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed partial class ModuleGraph(IReadOnlyDictionary<String, IReadOnlyList<String>> references)
{
    private const String ContractsSuffix = ".Contracts";

    [GeneratedRegex(@"<ProjectReference\s+Include=""(?<include>[^""]+)""")]
    private static partial Regex ProjectReferencePattern { get; }

    public static Result<ModuleGraph> Read(IFileSystem fileSystem, AppLayout app) =>
        fileSystem
            .GetDirectories(ScaffoldedModule.ModulesFolder, "*")
            .Select(Path.GetFileName)
            .OfType<String>()
            .Select(moduleName => ReadNeighbours(fileSystem, app, moduleName)
                .Map(neighbours => KeyValuePair.Create(moduleName, neighbours)))
            .CombineAll()
            .Map(modules => new ModuleGraph(modules.ToDictionary(StringComparer.Ordinal)));

    public static Optional<String> FindModuleOfContracts(String include, String projectPrefix)
    {
        var projectName = Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
        var modulePrefix = $"{projectPrefix}.Modules.";

        return projectName.Length > modulePrefix.Length + ContractsSuffix.Length
            && projectName.StartsWith(modulePrefix, StringComparison.Ordinal)
            && projectName.EndsWith(ContractsSuffix, StringComparison.Ordinal)
            ? projectName[modulePrefix.Length..^ContractsSuffix.Length]
            : Optional<String>.None();
    }

    public Boolean References(String from, String to) =>
        GetReferences(from)
            .Contains(to, StringComparer.Ordinal);

    public Optional<IReadOnlyList<String>> FindPath(String from, String to)
    {
        var previous = new Dictionary<String, String>(StringComparer.Ordinal) { [from] = from };
        var pending = new Queue<String>([from]);
        while (pending.TryDequeue(out var current))
        {
            if (current == to)
            {
                return Optional<IReadOnlyList<String>>.Some(BuildPath(previous, from, to));
            }

            foreach (var next in GetUnvisitedReferences(current, previous))
            {
                previous[next] = current;
                pending.Enqueue(next);
            }
        }

        return Optional<IReadOnlyList<String>>.None();
    }

    private IReadOnlyList<String> GetReferences(String moduleName) => references.GetValueOrDefault(moduleName, []);

    private IEnumerable<String> GetUnvisitedReferences(String moduleName, Dictionary<String, String> previous) =>
        GetReferences(moduleName)
            .Where(next => !previous.ContainsKey(next));

    private static List<String> BuildPath(Dictionary<String, String> previous, String from, String to)
    {
        var path = new List<String> { to };
        for (var current = to; current != from; current = previous[current])
        {
            path.Insert(0, previous[current]);
        }

        return path;
    }

    private static Result<IReadOnlyList<String>> ReadNeighbours(
        IFileSystem fileSystem,
        AppLayout app,
        String moduleName)
    {
        var projectName = $"{app.ProjectPrefix}.Modules.{moduleName}";
        var projectPath = $"{ScaffoldedModule.ModulesFolder}/{moduleName}/{projectName}/{projectName}.csproj";

        return fileSystem.FileExists(projectPath)
            ? fileSystem
                .ReadTextFile(projectPath)
                .Map(moduleProject => FindNeighbours(moduleProject.Text, app.ProjectPrefix, moduleName))
            : Result.Success<IReadOnlyList<String>>([]);
    }

    private static IReadOnlyList<String> FindNeighbours(String projectText, String projectPrefix, String moduleName) =>
        [
            .. ProjectReferencePattern
                .Matches(projectText)
                .Select(match => FindModuleOfContracts(match.Groups["include"].Value, projectPrefix))
                .Where(neighbour => neighbour.HasValue && neighbour.Value != moduleName)
                .Select(neighbour => neighbour.Value),
        ];
}
