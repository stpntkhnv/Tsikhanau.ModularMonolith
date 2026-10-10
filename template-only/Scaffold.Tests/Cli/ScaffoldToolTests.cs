using System.Diagnostics;
using System.Text;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Cli;

public sealed class ScaffoldToolTests : IDisposable
{
    private const String Usage = "usage: bash scripts/scaffold.sh module <Name>";

    private const String NewFilePath = "src/Modules/Notes/NotesModule.cs";

    private readonly SampleApp _app = SampleApp.Create();

    private readonly StringWriter _output = new();

    private readonly StringWriter _error = new();

    private readonly FakeProcessRunner _processRunner = FakeProcessRunner.Succeeding();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task Unknown_command_is_refused_with_the_usage()
    {
        var exitCode = await Tool.RunAsync(["frobnicate", "Notes"], CancellationToken);

        var error = _error.ToString();
        var output = _output.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: unknown command 'frobnicate'; {Usage}\n");
        output.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("module")]
    [InlineData("module Notes Tags")]
    public async Task Module_command_without_exactly_one_name_is_refused_with_the_usage(String commandLine)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await Tool.RunAsync(commandLine.Split(' '), CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: module takes exactly one name; {Usage}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Missing_command_is_refused_with_the_usage()
    {
        var exitCode = await Tool.RunAsync([], CancellationToken);

        var error = _error.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: no command given; {Usage}\n");
    }

    [Fact]
    public async Task Command_runs_its_plan_as_a_transaction_and_reports_it_done()
    {
        var command = new PlannedCommand(app => PlanNewModule(app));

        var exitCode = await Tool.RunAsync(command, CancellationToken);

        var output = _output.ToString();
        var error = _error.ToString();
        var program = _app.Read(SampleApp.ProgramPath);
        var newFile = _app.Read(NewFilePath);

        exitCode.ShouldBe(0);
        output.ShouldBe("scaffold: done; next, run ./gate.sh and commit\n");
        error.ShouldBeEmpty();
        program.ShouldEndWith("// Notes\n");
        newFile.ShouldBe("sealed class NotesModule;\n");
        _processRunner.Steps.ShouldBe(["bash scripts/add-migration.sh Notes Initial"]);
    }

    [Fact]
    public async Task Command_that_cannot_plan_changes_nothing()
    {
        var before = _app.TakeSnapshot();
        var command = new PlannedCommand(_ => Error.Failure("scaffold.exists", "src/Modules already holds Notes"));

        var exitCode = await Tool.RunAsync(command, CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe("scaffold: src/Modules already holds Notes\n");
        after.ShouldBe(before);
        _processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Folder_that_is_not_an_app_is_refused()
    {
        var folder = Directory.CreateTempSubdirectory("scaffold-empty-");
        var command = new PlannedCommand(app => PlanNewModule(app));

        var tool = new ScaffoldTool(new PhysicalFileSystem(folder.FullName), _processRunner, _output, _error);

        var exitCode = await tool.RunAsync(command, CancellationToken);

        folder.Delete(recursive: true);
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: expected one *.slnx in the app root, found 0: none; "
            + "expected one src/Bootstrapper/*.Bootstrapper/*.Bootstrapper.csproj, found 0: none\n");
    }

    [Fact]
    public async Task Built_tool_refuses_an_unknown_command_with_exit_code_2()
    {
        var startInfo = new ProcessStartInfo("dotnet", [ToolAssemblyPath, "frobnicate"])
        {
            WorkingDirectory = _app.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("cannot start dotnet");

        var error = await process.StandardError.ReadToEndAsync(CancellationToken);
        var output = await process.StandardOutput.ReadToEndAsync(CancellationToken);
        await process.WaitForExitAsync(CancellationToken);

        process.ExitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: unknown command 'frobnicate'; {Usage}\n");
        output.ShouldBeEmpty();
    }

    private static String ToolAssemblyPath => Path.Combine(AppContext.BaseDirectory, "Tsikhanau.Monolith.Scaffold.dll");

    private ScaffoldTool Tool => new(_app.FileSystem, _processRunner, _output, _error);

    private static Result<ScaffoldPlan> PlanNewModule(AppLayout app) =>
        app.Program
            .PlanEdit(text => text + "// Notes\n")
            .Map(programEdit => new ScaffoldPlan(
                [new NewFile(NewFilePath, "sealed class NotesModule;\n")],
                [programEdit],
                [new ExternalStep("bash", ["scripts/add-migration.sh", "Notes", "Initial"])]));

    private sealed class PlannedCommand(Func<AppLayout, Result<ScaffoldPlan>> plan) : IScaffoldCommand
    {
        public Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem) => plan(app);
    }
}
