using System.Text.RegularExpressions;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Apps;

internal sealed partial class AppLayoutReader(IFileSystem fileSystem)
{
    private const String HostProjectSuffix = ".Bootstrapper.csproj";

    private const String HostProjectPattern = $"src/Bootstrapper/*.Bootstrapper/*{HostProjectSuffix}";

    private const String AppHostFileName = "AppHost.cs";

    private const String AppHostPattern = $"src/Aspire/*.AppHost/{AppHostFileName}";

    private const String MissingPlatformUsing =
        "has no 'using <App>.Bootstrapper.Platform;', which names the root namespace";

    private const String DatabaseNameRule =
        "which cannot name the module roles <database>__<schema>: it must be groups of a-z and 0-9 joined by "
        + "single underscores, and be neither pg nor start with pg_, which PostgreSQL reserves for its own roles";

    [GeneratedRegex(@"^using (?<root>[A-Za-z_][\w.]*)\.Bootstrapper\.Platform;\r?$", RegexOptions.Multiline)]
    private static partial Regex PlatformUsingPattern { get; }

    [GeneratedRegex(@"\bAddDatabase\([ \t]*""database""[ \t]*,[ \t]*databaseName:[ \t]*""(?<name>[^""\r\n]*)""")]
    private static partial Regex AppDatabasePattern { get; }

    [GeneratedRegex(@"\A(?!pg(_|\z))[a-z0-9]+(_[a-z0-9]+)*\z")]
    private static partial Regex DatabaseNamePattern { get; }

    public Result<AppLayout> Read() =>
        Result
            .Collect(GetSolutionPath())
            .And(GetHostProjectPath())
            .And(GetAppHostPath())
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

    private Result<String> GetAppHostPath() =>
        GetSingle(
            AppHostPattern,
            [
                .. fileSystem
                    .GetDirectories("src/Aspire", "*.AppHost")
                    .SelectMany(folder => fileSystem.GetFiles(folder, AppHostFileName)),
            ]);

    private static Result<String> GetSingle(String description, IReadOnlyList<String> paths) =>
        paths is [var path]
            ? path
            : Error.Failure(
                "scaffold.app-layout",
                $"expected one {description}, found {paths.Count}: {BuildPathList(paths)}");

    private static String BuildPathList(IReadOnlyList<String> paths) =>
        paths.Count == 0 ? "none" : String.Join(", ", paths);

    private Result<AppLayout> ReadLayout(String solutionPath, String hostProjectPath, String appHostPath)
    {
        var hostFolder = Path.GetDirectoryName(hostProjectPath) ?? String.Empty;

        return Result
            .Collect(fileSystem.ReadTextFile(solutionPath))
            .And(fileSystem.ReadTextFile(hostProjectPath))
            .And(fileSystem.ReadTextFile(Path.Combine(hostFolder, "Program.cs")))
            .And(fileSystem.ReadTextFile(Path.Combine(hostFolder, "appsettings.json")))
            .And(fileSystem.ReadTextFile(appHostPath))
            .Bind((solution, hostProject, program, appSettings, appHost) =>
                Result
                    .Collect(ReadRootNamespace(program))
                    .And(ReadDatabaseName(appHost))
                    .Map((rootNamespace, databaseName) => new AppLayout(
                        ReadProjectPrefix(hostProjectPath),
                        rootNamespace,
                        databaseName,
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

    private static Result<String> ReadDatabaseName(TextFile appHost)
    {
        var matches = AppDatabasePattern.Matches(appHost.Text);

        return matches is [var match]
            ? ValidateDatabaseName(appHost.Path, match.Groups["name"].Value)
            : Error.Failure(
                "scaffold.app-layout",
                $"{appHost.Path} must name the app database once, as "
                + $"AddDatabase(\"database\", databaseName: \"<name>\"); found {matches.Count}");
    }

    private static Result<String> ValidateDatabaseName(String appHostPath, String databaseName) =>
        Result
            .Success(databaseName)
            .Ensure(KeepsDatabaseNameRule, BuildDatabaseNameError(appHostPath, databaseName, DatabaseNameRule))
            .Ensure(
                FitsGroupLength,
                BuildDatabaseNameError(appHostPath, databaseName, BuildGroupLengthRule(databaseName)));

    private static Boolean KeepsDatabaseNameRule(String databaseName) => DatabaseNamePattern.IsMatch(databaseName);

    private static Boolean FitsGroupLength(String databaseName) =>
        RoleNames.BuildGroup(databaseName).Length <= RoleNames.MaxLength;

    private static String BuildGroupLengthRule(String databaseName)
    {
        var group = RoleNames.BuildGroup(databaseName);

        return $"which makes the group role {group} {group.Length} characters long, more than the "
            + $"{RoleNames.MaxLength} PostgreSQL allows in a role name: shorten it to at most "
            + $"{RoleNames.MaxLength - RoleNames.BuildGroup(String.Empty).Length} characters";
    }

    private static Error BuildDatabaseNameError(String appHostPath, String databaseName, String rule) =>
        Error.Failure("scaffold.app-layout", $"{appHostPath} names the database '{databaseName}', {rule}");
}
