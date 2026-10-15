using System.Diagnostics;
using System.Security.Cryptography;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal sealed class SampleApp : IDisposable
{
    public const String Name = "Acme.Shop";

    public const String DatabaseName = "acme_shop";

    public const String SolutionPath = "Acme.Shop.slnx";

    public const String HostProjectPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/Acme.Shop.Bootstrapper.csproj";

    public const String ProgramPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/Program.cs";

    public const String AppSettingsPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/appsettings.json";

    public const String IntegrationTestsProjectPath = "src/Tests/IntegrationTests/Acme.Shop.IntegrationTests.csproj";

    public const String AppHostPath = "src/Aspire/Acme.Shop.AppHost/AppHost.cs";

    private const Int32 DeleteAttempts = 5;

    private const String TemplateName = "ModularMonolith";

    private const String TemplateDatabaseName = "modularmonolith";

    private const String TemplateHostFolder = $"src/Bootstrapper/{TemplateName}.Bootstrapper";

    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(200);

    private SampleApp(String root) => Root = root;

    public static String Solution { get; } = ReadTemplateFile($"{TemplateName}.slnx");

    public static String HostProject { get; } =
        ReadTemplateFile($"{TemplateHostFolder}/{TemplateName}.Bootstrapper.csproj");

    public static String Program { get; } = ReadTemplateFile($"{TemplateHostFolder}/Program.cs");

    public static String AppSettings { get; } = ReadTemplateFile($"{TemplateHostFolder}/appsettings.json");

    public static String IntegrationTestsProject { get; } =
        ReadTemplateFile($"src/Tests/IntegrationTests/{TemplateName}.IntegrationTests.csproj");

    public static String AppHost { get; } = ReadTemplateFile($"src/Aspire/{TemplateName}.AppHost/AppHost.cs");

    public String Root { get; }

    public PhysicalFileSystem FileSystem => new(Root);

    public static SampleApp Create()
    {
        var app = new SampleApp(Directory.CreateTempSubdirectory("scaffold-app-").FullName);
        app.Write(SolutionPath, Solution);
        app.Write(HostProjectPath, HostProject);
        app.Write(ProgramPath, Program);
        app.Write(AppSettingsPath, AppSettings);
        app.Write(IntegrationTestsProjectPath, IntegrationTestsProject);
        app.Write(AppHostPath, AppHost);
        app.Write("src/Modules/.gitkeep", String.Empty);
        app.Write(".gitignore", "bin/\nobj/\n");
        app.RunGit("init", "-q", "-b", "main");
        app.Commit();
        return app;
    }

    public void Write(String relativePath, String content)
    {
        var path = ToFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Root);
        File.WriteAllText(path, content);
    }

    public String Read(String relativePath) => File.ReadAllText(ToFullPath(relativePath));

    public Boolean Exists(String relativePath) => Path.Exists(ToFullPath(relativePath));

    public void Delete(String relativePath) => File.Delete(ToFullPath(relativePath));

    public void Commit()
    {
        RunGit("add", "-A");
        RunGit("commit", "-q", "--allow-empty", "-m", "app");
    }

    public AppLayout ReadLayout() =>
        new AppLayoutReader(FileSystem)
            .Read()
            .Value;

    public TreeSnapshot TakeSnapshot() =>
        new(RunGit("status", "--porcelain=v1", "--untracked-files=all"), HashFiles());

    public void Dispose()
    {
        for (var attempt = 1; attempt < DeleteAttempts; attempt++)
        {
            try
            {
                Directory.Delete(Root, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(DeleteRetryDelay);
            }
        }

        Directory.Delete(Root, recursive: true);
    }

    private String ToFullPath(String relativePath) => Path.Combine(Root, relativePath);

    private static String ReadTemplateFile(String relativePath) =>
        File
            .ReadAllText(Path.Combine(TemplateFolder.Root, relativePath))
            .Replace(TemplateName, Name, StringComparison.Ordinal)
            .Replace(TemplateDatabaseName, DatabaseName, StringComparison.Ordinal);

    private String HashFiles() =>
        String.Join(
            '\n',
            Directory
                .EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path))
                .Where(IsOutsideGitAndBuildOutput)
                .Order(StringComparer.Ordinal)
                .Select(HashFile));

    private String HashFile(String relativePath) =>
        $"{relativePath} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ToFullPath(relativePath))))}";

    private static Boolean IsOutsideGitAndBuildOutput(String relativePath) =>
        !relativePath
            .Split(Path.DirectorySeparatorChar)
            .Any(part => part is ".git" or "bin" or "obj");

    private String RunGit(params String[] arguments)
    {
        using var process = Process.Start(CreateGitStartInfo(arguments))
            ?? throw new InvalidOperationException("cannot start git");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, error);

        return output;
    }

    private ProcessStartInfo CreateGitStartInfo(IReadOnlyList<String> arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.Environment.Keys
            .Where(name => name.StartsWith("GIT_", StringComparison.Ordinal))
            .ToList()
            .ForEach(name => startInfo.Environment.Remove(name));

        startInfo.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_COUNT"] = "3";
        startInfo.Environment["GIT_CONFIG_KEY_0"] = "gc.auto";
        startInfo.Environment["GIT_CONFIG_VALUE_0"] = "0";
        startInfo.Environment["GIT_CONFIG_KEY_1"] = "maintenance.auto";
        startInfo.Environment["GIT_CONFIG_VALUE_1"] = "false";
        startInfo.Environment["GIT_CONFIG_KEY_2"] = "gc.autoDetach";
        startInfo.Environment["GIT_CONFIG_VALUE_2"] = "false";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "test";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "test@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "test";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "test@example.com";

        return startInfo;
    }
}
