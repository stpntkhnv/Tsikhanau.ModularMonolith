using System.Diagnostics;
using System.Security.Cryptography;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Files;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal sealed class SampleApp : IDisposable
{
    public const String SolutionPath = "Acme.Shop.slnx";

    public const String HostProjectPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/Acme.Shop.Bootstrapper.csproj";

    public const String ProgramPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/Program.cs";

    public const String AppSettingsPath = "src/Bootstrapper/Acme.Shop.Bootstrapper/appsettings.json";

    public const String Solution = """
        <Solution>
          <Folder Name="/src/" />
          <Folder Name="/src/Bootstrapper/">
            <Project Path="src/Bootstrapper/Acme.Shop.Bootstrapper/Acme.Shop.Bootstrapper.csproj" />
          </Folder>
          <Folder Name="/src/Modules/" />
        </Solution>

        """;

    public const String HostProject = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <ItemGroup>
            <ProjectReference Include="..\..\Core\Infrastructure\Tsikhanau.Monolith.Infrastructure.csproj" />
          </ItemGroup>
        </Project>

        """;

    public const String Program = """
        using Acme.Shop.Bootstrapper.Platform;
        using Tsikhanau.Monolith.Api.Modules;

        var builder = WebApplication.CreateBuilder(args);
        builder.AddPlatform();
        builder
            .Build()
            .Run();

        """;

    public const String AppSettings = """
        {
          "App": {
            "Database": {
              "LoginPrefix": "acme_shop"
            }
          }
        }

        """;

    private SampleApp(String root) => Root = root;

    public String Root { get; }

    public PhysicalFileSystem FileSystem => new(Root);

    public static SampleApp Create()
    {
        var app = new SampleApp(Directory.CreateTempSubdirectory("scaffold-app-").FullName);
        app.Write(SolutionPath, Solution);
        app.Write(HostProjectPath, HostProject);
        app.Write(ProgramPath, Program);
        app.Write(AppSettingsPath, AppSettings);
        app.Write("src/Modules/.gitkeep", String.Empty);
        app.Write(".gitignore", "bin/\nobj/\n");
        app.RunGit("init", "-q", "-b", "main");
        app.RunGit("add", "-A");
        app.RunGit("commit", "-q", "-m", "app");
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

    public AppLayout ReadLayout() =>
        new AppLayoutReader(FileSystem)
            .Read()
            .Value;

    public TreeSnapshot TakeSnapshot() =>
        new(RunGit("status", "--porcelain=v1", "--untracked-files=all"), HashFiles());

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private String ToFullPath(String relativePath) => Path.Combine(Root, relativePath);

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

    private ProcessStartInfo CreateGitStartInfo(IReadOnlyList<String> arguments) =>
        new("git", arguments)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["GIT_CONFIG_GLOBAL"] = "/dev/null",
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_AUTHOR_NAME"] = "test",
                ["GIT_AUTHOR_EMAIL"] = "test@example.com",
                ["GIT_COMMITTER_NAME"] = "test",
                ["GIT_COMMITTER_EMAIL"] = "test@example.com",
            },
        };
}
