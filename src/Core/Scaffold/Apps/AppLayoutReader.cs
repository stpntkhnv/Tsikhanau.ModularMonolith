using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Apps;

internal sealed partial class AppLayoutReader(IFileSystem fileSystem)
{
    private const String HostProjectSuffix = ".Bootstrapper.csproj";

    private const String HostProjectPattern = $"src/Bootstrapper/*.Bootstrapper/*{HostProjectSuffix}";

    private const String MissingPlatformUsing =
        "has no 'using <App>.Bootstrapper.Platform;', which names the root namespace";

    [GeneratedRegex(@"^using (?<root>[A-Za-z_][\w.]*)\.Bootstrapper\.Platform;\r?$", RegexOptions.Multiline)]
    private static partial Regex PlatformUsingPattern { get; }

    public Result<AppLayout> Read() =>
        Result
            .Collect(GetSolutionPath())
            .And(GetHostProjectPath())
            .Bind(ReadLayout);

    private Result<String> GetSolutionPath() =>
        GetSingle("*.slnx in the app root", fileSystem.GetFiles(".", "*.slnx"));

    private Result<String> GetHostProjectPath() =>
        GetSingle(
            HostProjectPattern,
            [
                .. fileSystem
                    .GetDirectories("src/Bootstrapper", "*.Bootstrapper")
                    .SelectMany(folder => fileSystem.GetFiles(folder, $"*{HostProjectSuffix}")),
            ]);

    private static Result<String> GetSingle(String description, IReadOnlyList<String> paths) =>
        paths is [var path]
            ? path
            : Error.Failure(
                "scaffold.app-layout",
                $"expected one {description}, found {paths.Count}: {BuildPathList(paths)}");

    private static String BuildPathList(IReadOnlyList<String> paths) =>
        paths.Count == 0 ? "none" : String.Join(", ", paths);

    private Result<AppLayout> ReadLayout(String solutionPath, String hostProjectPath)
    {
        var hostFolder = Path.GetDirectoryName(hostProjectPath) ?? String.Empty;

        return Result
            .Collect(fileSystem.ReadTextFile(solutionPath))
            .And(fileSystem.ReadTextFile(hostProjectPath))
            .And(fileSystem.ReadTextFile(Path.Combine(hostFolder, "Program.cs")))
            .And(fileSystem.ReadTextFile(Path.Combine(hostFolder, "appsettings.json")))
            .Bind((solution, hostProject, program, appSettings) =>
                ReadRootNamespace(program)
                    .Map(rootNamespace => new AppLayout(
                        ReadProjectPrefix(hostProjectPath),
                        rootNamespace,
                        solution,
                        hostProject,
                        program,
                        appSettings)));
    }

    private static String ReadProjectPrefix(String hostProjectPath) =>
        Path.GetFileName(hostProjectPath)[..^HostProjectSuffix.Length];

    private static Result<String> ReadRootNamespace(TextFile program) =>
        PlatformUsingPattern.Match(program.Text) is { Success: true } match
            ? match.Groups["root"].Value
            : Error.Failure("scaffold.app-layout", $"{program.Path} {MissingPlatformUsing}");
}
