using System.Diagnostics;
using System.Text;
using Tsikhanau.Monolith.Scaffold.Apps;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Monolith.Scaffold.Tests.Fakes;
using Tsikhanau.Monolith.Scaffold.Tests.Samples;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Cli;

public sealed class ScaffoldToolTests : IDisposable
{
    private const String ModuleSynopsis = "bash scripts/scaffold.sh module <Name> [--settings]";

    private const String FeatureSynopsis =
        "bash scripts/scaffold.sh feature <Module> <Name> --kind command|query|public-query|event-handler|"
        + "internal-command [--endpoint none|get|post|put|delete] [--route <path>] [--event <Type>]";

    private const String Usage = $"usage: {ModuleSynopsis}, or {FeatureSynopsis}";

    private const String RouteWithoutEndpoint = "--route needs an endpoint: --endpoint get, post, put or delete";

    private const String SegmentRule = "a segment is kebab-case words (archive, note-tags) or an id {name:guid}";

    private const String GuidIdRule = "an id is a Guid v7, so its parameter is {name:guid}";

    private const String NewFilePath = "src/Modules/Notes/NotesModule.cs";

    private const String TagsFilePath = "src/Modules/Tags/TagsModule.cs";

    private const String LockedOut =
        "scaffold: cannot take the scaffold lock src/Core/Scaffold/obj/scaffold.lock, which another scaffold "
        + "command in this app holds while it runs; run this one when that one ends: ";

    private static readonly Error ModuleExists = Error.Failure("scaffold.exists", "src/Modules already holds Notes");

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
    [InlineData("module --settings")]
    [InlineData("module --settings Notes")]
    [InlineData("module Notes --setting")]
    [InlineData("module Notes --settings --settings")]
    public async Task Module_command_without_one_name_and_at_most_the_settings_option_is_refused(String commandLine)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await Tool.RunAsync(commandLine.Split(' '), CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: module takes one name and optionally --settings; usage: {ModuleSynopsis}\n");
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("feature", "feature takes a module name and a feature name, then its options")]
    [InlineData("feature Notes", "feature takes a module name and a feature name, then its options")]
    [InlineData("feature --kind command Notes Pin", "feature takes a module name and a feature name, then its options")]
    [InlineData("feature Notes Pin", "feature needs --kind")]
    [InlineData("feature Notes Pin --endpoint post", "feature needs --kind")]
    [InlineData("feature Notes Pin --kind", "--kind needs a value")]
    [InlineData("feature Notes Pin --kind --endpoint post", "--kind needs a value")]
    [InlineData("feature Notes Pin --kind command --kind command", "--kind is given twice")]
    [InlineData("feature Notes Pin --kind command --endpoint put --endpoint post", "--endpoint is given twice")]
    [InlineData("feature Notes Pin --kind command --force yes", "feature does not know '--force'")]
    [InlineData("feature Notes Pin --kind command post", "feature does not know 'post'")]
    [InlineData("feature Notes Pin --kind patch", "--kind 'patch' is not a feature kind")]
    [InlineData("feature Notes Pin --kind Command", "--kind 'Command' is not a feature kind")]
    [InlineData("feature Notes Pin --kind Query", "--kind 'Query' is not a feature kind")]
    [InlineData("feature Notes Pin --kind public_query", "--kind 'public_query' is not a feature kind")]
    [InlineData("feature Notes Pin --kind command --endpoint patch", "--endpoint 'patch' is not an endpoint method")]
    [InlineData("feature Notes Pin --kind Event-handler --event E", "--kind 'Event-handler' is not a feature kind")]
    [InlineData("feature Notes Pin --kind event_handler --event E", "--kind 'event_handler' is not a feature kind")]
    [InlineData("feature Notes Pin --kind Internal-command", "--kind 'Internal-command' is not a feature kind")]
    [InlineData("feature Notes Pin --kind internal_command", "--kind 'internal_command' is not a feature kind")]
    [InlineData("feature Notes Pin --kind event-handler --event", "--event needs a value")]
    [InlineData("feature Notes Pin --event --kind event-handler", "--event needs a value")]
    [InlineData("feature Notes Pin --kind event-handler --event A --event B", "--event is given twice")]
    [InlineData("feature Notes Pin --kind command --endpoint post --route", "--route needs a value")]
    [InlineData("feature Notes Pin --kind command --endpoint post --route /a --route /b", "--route is given twice")]
    public async Task Feature_command_line_scaffold_cannot_read_is_refused_with_the_usage(
        String commandLine,
        String problem)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await Tool.RunAsync(commandLine.Split(' '), CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: {problem}; usage: {FeatureSynopsis}\n");
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("", "--kind query", RouteWithoutEndpoint)]
    [InlineData("", "--kind command --endpoint none", RouteWithoutEndpoint)]
    [InlineData("/archive", "--kind command", RouteWithoutEndpoint)]
    [InlineData(
        "notes",
        "--kind query --endpoint get",
        "route 'notes' must be empty, the collection of the module, or start with /")]
    [InlineData("/notes/", "--kind command --endpoint post", "route '/notes/' must not end in /")]
    [InlineData("/{id}", "--kind query --endpoint get", $"route '/{{id}}' has the parameter {{id}}: {GuidIdRule}")]
    [InlineData(
        "/{id:int}",
        "--kind query --endpoint get",
        $"route '/{{id:int}}' has the parameter {{id:int}}: {GuidIdRule}")]
    [InlineData(
        "/Bad_Segment",
        "--kind command --endpoint put",
        $"route '/Bad_Segment' has the segment 'Bad_Segment': {SegmentRule}")]
    [InlineData(
        "/{id:guid}/{id:guid}",
        "--kind command --endpoint delete",
        "route '/{id:guid}/{id:guid}' names the parameter id twice: each parameter needs its own name")]
    [InlineData(
        "/{noteId:guid}/tags/{noteid:guid}",
        "--kind query --endpoint get",
        "route '/{noteId:guid}/tags/{noteid:guid}' names the parameter noteId twice: "
        + "each parameter needs its own name")]
    public async Task Route_without_an_endpoint_or_that_breaks_a_route_rule_is_refused_with_the_usage(
        String route,
        String kindAndMethod,
        String problem)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await Tool.RunAsync(
            ["feature", "Notes", "PinNote", .. kindAndMethod.Split(' '), "--route", route],
            CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(2);
        error.ShouldBe($"scaffold: {problem}; usage: {FeatureSynopsis}\n");
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
        var scaffold = new PlannedScaffold(app => PlanNewModule(app));

        var exitCode = await Tool.ScaffoldAsync(scaffold, CancellationToken);

        var output = _output.ToString();
        var error = _error.ToString();
        var program = _app.Read(SampleApp.ProgramPath);
        var newFile = _app.Read(NewFilePath);

        exitCode.ShouldBe(0);
        output.ShouldBe("scaffold: done; next, run ./gate.sh and commit\n");
        error.ShouldBeEmpty();
        program.ShouldEndWith("// Notes\n", Case.Sensitive);
        newFile.ShouldBe("sealed class NotesModule;\n");
        _processRunner.Steps.ShouldBe(["bash scripts/add-migration.sh Notes Initial"]);
    }

    [Fact]
    public async Task Second_command_is_refused_while_one_runs_and_runs_once_it_ends()
    {
        using var secondError = new StringWriter();
        var secondTool = new ScaffoldTool(
            _app.FileSystem,
            FakeProcessRunner.Succeeding(),
            TextWriter.Null,
            secondError);

        var tagsScaffold = new PlannedScaffold(_ => PlanTagsModule());
        var secondExitCode = 0;
        var migration = new FakeProcessRunner(async (_, cancellationToken) =>
        {
            secondExitCode = await secondTool.ScaffoldAsync(tagsScaffold, cancellationToken);
            return Result.Success();
        });

        var exitCode = await new ScaffoldTool(_app.FileSystem, migration, _output, _error)
            .ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        var tagsExistedWhileNotesRan = _app.Exists(TagsFilePath);
        var nextExitCode = await secondTool.ScaffoldAsync(tagsScaffold, CancellationToken);
        var tagsExistAfterwards = _app.Exists(TagsFilePath);

        exitCode.ShouldBe(0, _error.ToString());
        secondExitCode.ShouldBe(1);
        secondError
            .ToString()
            .ShouldStartWith(LockedOut, Case.Sensitive);

        tagsExistedWhileNotesRan.ShouldBeFalse();
        nextExitCode.ShouldBe(0, secondError.ToString());
        tagsExistAfterwards.ShouldBeTrue();
    }

    [Fact]
    public async Task Second_command_is_refused_while_the_first_plans()
    {
        using var contenderError = new StringWriter();
        var contenderRuns = new List<Task<Int32>>();
        var scaffold = new PlannedScaffold(app =>
        {
            contenderRuns.Add(RunContenderAsync(contenderError));
            return PlanNewModule(app);
        });

        var exitCode = await Tool.ScaffoldAsync(scaffold, CancellationToken);

        var contenderExitCodes = await Task.WhenAll(contenderRuns);
        var contenderMessage = contenderError.ToString();

        exitCode.ShouldBe(0, _error.ToString());
        contenderExitCodes.ShouldBe([1]);
        contenderMessage.ShouldStartWith(LockedOut, Case.Sensitive);
    }

    [Fact]
    public async Task Second_command_is_refused_while_the_first_rolls_back()
    {
        using var contenderError = new StringWriter();
        var contenderRuns = new List<Task<Int32>>();
        var fileSystem = new CallbackFileSystem(_app.Root, () => contenderRuns.Add(RunContenderAsync(contenderError)));
        var tool = new ScaffoldTool(fileSystem, FailingProcessRunner, _output, _error);

        var exitCode = await tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        var contenderExitCodes = await Task.WhenAll(contenderRuns);
        var contenderMessage = contenderError.ToString();

        exitCode.ShouldBe(1);
        contenderExitCodes.ShouldBe([1]);
        contenderMessage.ShouldStartWith(LockedOut, Case.Sensitive);
    }

    [Fact]
    public async Task Second_command_runs_while_the_first_reports_it_done()
    {
        using var contenderError = new StringWriter();
        var contenderRuns = new List<Task<Int32>>();
        using var output = new CallbackTextWriter(() => contenderRuns.Add(RunContenderAsync(contenderError)));
        var tool = new ScaffoldTool(_app.FileSystem, _processRunner, output, _error);

        var exitCode = await tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        var contenderExitCodes = await Task.WhenAll(contenderRuns);

        exitCode.ShouldBe(0, _error.ToString());
        contenderExitCodes.ShouldBe([0], contenderError.ToString());
    }

    [Fact]
    public async Task Second_command_runs_while_the_first_reports_its_failure()
    {
        using var contenderError = new StringWriter();
        var contenderRuns = new List<Task<Int32>>();
        using var error = new CallbackTextWriter(() => contenderRuns.Add(RunContenderAsync(contenderError)));
        var tool = new ScaffoldTool(_app.FileSystem, _processRunner, _output, error);

        var exitCode = await tool.ScaffoldAsync(new PlannedScaffold(_ => ModuleExists), CancellationToken);

        var contenderExitCodes = await Task.WhenAll(contenderRuns);

        exitCode.ShouldBe(1);
        contenderExitCodes.ShouldBe([0], contenderError.ToString());
    }

    [Fact]
    public async Task Next_command_runs_after_one_that_cannot_plan()
    {
        var failedExitCode = await Tool.ScaffoldAsync(new PlannedScaffold(_ => ModuleExists), CancellationToken);
        var nextExitCode = await Tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        failedExitCode.ShouldBe(1);
        nextExitCode.ShouldBe(0, _error.ToString());
    }

    [Fact]
    public async Task Next_command_runs_after_one_whose_transaction_failed()
    {
        var failedExitCode = await new ScaffoldTool(_app.FileSystem, FailingProcessRunner, _output, _error)
            .ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        var nextExitCode = await Tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        failedExitCode.ShouldBe(1);
        nextExitCode.ShouldBe(0, _error.ToString());
    }

    [Fact]
    public async Task Unexpected_exception_passes_through_the_tool_and_leaves_the_lock_free()
    {
        var before = _app.TakeSnapshot();
        var bug = new InvalidOperationException("bug");
        var tool = new ScaffoldTool(_app.FileSystem, new FakeProcessRunner((_, _) => throw bug), _output, _error);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken));

        var after = _app.TakeSnapshot();
        var output = _output.ToString();
        var error = _error.ToString();
        var nextExitCode = await Tool.ScaffoldAsync(new PlannedScaffold(PlanNewModule), CancellationToken);

        exception.ShouldBeSameAs(bug);
        after.ShouldBe(before);
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
        nextExitCode.ShouldBe(0, _error.ToString());
    }

    [Fact]
    public async Task Command_that_cannot_plan_changes_nothing()
    {
        var before = _app.TakeSnapshot();
        var scaffold = new PlannedScaffold(_ => ModuleExists);

        var exitCode = await Tool.ScaffoldAsync(scaffold, CancellationToken);

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
        var scaffold = new PlannedScaffold(app => PlanNewModule(app));

        var tool = new ScaffoldTool(new PhysicalFileSystem(folder.FullName), _processRunner, _output, _error);

        var exitCode = await tool.ScaffoldAsync(scaffold, CancellationToken);

        folder.Delete(recursive: true);
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: expected one *.slnx in the app root, found 0: none; "
            + "expected one src/Bootstrapper/*.Bootstrapper/*.Bootstrapper.csproj, found 0: none; "
            + "expected one src/Aspire/*.AppHost/AppHost.cs, found 0: none\n");
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

    private static FakeProcessRunner FailingProcessRunner =>
        new((_, _) => Task.FromResult(Result.Failure(Error.Failure("step", "the build failed"))));

    private Task<Int32> RunContenderAsync(TextWriter errorTextWriter) =>
        new ScaffoldTool(_app.FileSystem, FakeProcessRunner.Succeeding(), TextWriter.Null, errorTextWriter)
            .ScaffoldAsync(new PlannedScaffold(_ => new ScaffoldPlan([], [], [])), CancellationToken);

    private static Result<ScaffoldPlan> PlanNewModule(AppLayout app) =>
        app.Program
            .PlanEdit(text => text + "// Notes\n")
            .Map(programEdit => new ScaffoldPlan(
                [new NewFile(NewFilePath, "sealed class NotesModule;\n")],
                [programEdit],
                [new ExternalStep("bash", ["scripts/add-migration.sh", "Notes", "Initial"])]));

    private static Result<ScaffoldPlan> PlanTagsModule() =>
        new ScaffoldPlan([new NewFile(TagsFilePath, "sealed class TagsModule;\n")], [], []);

    private sealed class PlannedScaffold(Func<AppLayout, Result<ScaffoldPlan>> plan) : IScaffold
    {
        public Result<ScaffoldPlan> Plan(AppLayout app, IFileSystem fileSystem) => plan(app);
    }

    private sealed class CallbackTextWriter(Action onFirstWrite) : TextWriter
    {
        private Boolean _isWritten;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(Char value)
        {
            if (!_isWritten)
            {
                _isWritten = true;
                onFirstWrite();
            }
        }
    }

    private sealed class CallbackFileSystem(String root, Action onDeleteFile) : IFileSystem
    {
        private readonly PhysicalFileSystem _fileSystem = new(root);

        public Boolean FileExists(String path) => _fileSystem.FileExists(path);

        public Boolean DirectoryExists(String path) => _fileSystem.DirectoryExists(path);

        public IReadOnlyList<String> GetFiles(String folder, String searchPattern) =>
            _fileSystem.GetFiles(folder, searchPattern);

        public IReadOnlyList<String> GetDirectories(String folder, String searchPattern) =>
            _fileSystem.GetDirectories(folder, searchPattern);

        public Byte[] ReadAllBytes(String path) => _fileSystem.ReadAllBytes(path);

        public void CreateDirectory(String path) => _fileSystem.CreateDirectory(path);

        public void WriteAllBytes(String path, Byte[] content) => _fileSystem.WriteAllBytes(path, content);

        public void DeleteFile(String path)
        {
            onDeleteFile();
            _fileSystem.DeleteFile(path);
        }

        public void DeleteDirectory(String path) => _fileSystem.DeleteDirectory(path);

        public IDisposable Lock(String path) => _fileSystem.Lock(path);
    }
}
