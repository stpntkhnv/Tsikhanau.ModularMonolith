using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Transactions;

public sealed class TransactionTests : IDisposable
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String ModuleFolder = "src/Modules/Notes";

    private const String ModuleProjectFolder = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String ContractsFolder = "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts";

    private const String ModuleClassPath = "src/Modules/Notes/Acme.Shop.Modules.Notes/NotesModule.cs";

    private const String FileInAnExistingFolder = "src/Modules/Notes.md";

    private const Int32 FirstNewFile = 1;

    private const Int32 NewFileInAnExistingFolder = 4;

    private const Int32 LastNewFile = 7;

    private const Int32 FirstEdit = 8;

    private const Int32 MiddleEdit = 9;

    private const Int32 LastEdit = 11;

    private static readonly IReadOnlyList<NewFile> NewFiles =
    [
        new($"{ContractsFolder}/Acme.Shop.Modules.Notes.Contracts.csproj", "<Project />\n"),
        new($"{ContractsFolder}/INotesQueryFacade.cs", "interface INotesQueryFacade;\n"),
        new($"{ModuleProjectFolder}/Acme.Shop.Modules.Notes.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n"),
        new(FileInAnExistingFolder, "# Notes\n"),
        new(ModuleClassPath, "sealed class NotesModule;\n"),
        new($"{ModuleProjectFolder}/Infrastructure/NotesModuleFacade.cs", "sealed class NotesModuleFacade;\n"),
        new($"{ModuleProjectFolder}/Infrastructure/Persistence/NotesDbContext.cs", "sealed class NotesDbContext;\n"),
    ];

    private static readonly IReadOnlyList<String> PlannedWrites =
    [
        .. NewFiles.Select(newFile => newFile.Path),
        SampleApp.SolutionPath,
        SampleApp.HostProjectPath,
        SampleApp.ProgramPath,
        SampleApp.AppSettingsPath,
    ];

    private static readonly ExternalStep MigrationStep = new("bash", ["scripts/add-migration.sh", "Notes", "Initial"]);

    private readonly SampleApp _app = SampleApp.Create();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Successful_run_creates_the_files_applies_the_edits_and_runs_the_steps()
    {
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await RunAsync(_app.FileSystem, processRunner);

        IReadOnlyList<String> createdFiles = [.. NewFiles.Select(newFile => _app.Read(newFile.Path))];
        var program = _app.Read(SampleApp.ProgramPath);
        var appSettings = _app.Read(SampleApp.AppSettingsPath);

        outcome.ShouldBe(Result.Success());
        createdFiles.ShouldBe(NewFiles.Select(newFile => newFile.Content));
        program.ShouldContain("builder.AddPlatform();\nbuilder.AddModule<NotesModule>();\n", Case.Sensitive);
        appSettings.ShouldContain("\"LoginPrefix\": \"acme_shop\",\n      \"Notes\": true", Case.Sensitive);
        processRunner.Steps.ShouldBe(["bash scripts/add-migration.sh Notes Initial"]);
    }

    [Fact]
    public async Task Failed_step_deletes_what_it_wrote_in_the_new_folders_and_restores_the_edits()
    {
        var before = _app.TakeSnapshot();
        var processRunner = new FakeProcessRunner((_, _) =>
        {
            _app.Write($"{ModuleProjectFolder}/Infrastructure/Migrations/20261010000000_Initial.cs", "migration");
            _app.Write($"{ModuleProjectFolder}/obj/project.assets.json", "{}");
            _app.Write("src/Bootstrapper/Acme.Shop.Bootstrapper/obj/project.assets.json", "{}");
            return Task.FromResult(Result.Failure(Error.Failure("step", "the build failed")));
        });

        var outcome = await RunAsync(_app.FileSystem, processRunner);

        var after = _app.TakeSnapshot();
        var hasModuleFolder = _app.Exists(ModuleFolder);
        var hasHostBuildOutput = _app.Exists("src/Bootstrapper/Acme.Shop.Bootstrapper/obj/project.assets.json");
        var message = ReadMessage(outcome);

        message.ShouldBe($"the build failed; {RolledBack}");
        after.ShouldBe(before);
        hasModuleFolder.ShouldBeFalse();
        hasHostBuildOutput.ShouldBeTrue();
    }

    [Theory]
    [InlineData(FirstNewFile)]
    [InlineData(NewFileInAnExistingFolder)]
    [InlineData(LastNewFile)]
    [InlineData(FirstEdit)]
    [InlineData(MiddleEdit)]
    [InlineData(LastEdit)]
    public async Task Failed_write_leaves_the_tree_as_it_was(Int32 failingWrite)
    {
        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await RunAsync(new FailingFileSystem(_app.Root, failingWrite: failingWrite), processRunner);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"cannot write {PlannedWrites[failingWrite - 1]}: disk full; {RolledBack}");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ModuleFolder)]
    [InlineData($"{ModuleProjectFolder}/Infrastructure")]
    public async Task Folder_that_cannot_be_created_leaves_the_tree_as_it_was(String failingFolder)
    {
        var before = _app.TakeSnapshot();
        var fileSystem = new FailingFileSystem(_app.Root, failingFolder: failingFolder);

        var outcome = await RunAsync(fileSystem, FakeProcessRunner.Succeeding());

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldStartWith($"cannot create the folder {failingFolder}: read-only folder", Case.Sensitive);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Run_stopped_during_a_step_leaves_the_tree_as_it_was()
    {
        var before = _app.TakeSnapshot();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        var processRunner = new FakeProcessRunner(async (_, cancellationToken) =>
        {
            await cancellation.CancelAsync();
            return cancellationToken.IsCancellationRequested
                ? Result.Failure(Error.Failure("step", "the step was stopped"))
                : Result.Success();
        });

        var outcome = await RunAsync(_app.FileSystem, processRunner, cancellation.Token);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"the step was stopped; {RolledBack}");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Run_stopped_before_the_first_write_writes_nothing()
    {
        var before = _app.TakeSnapshot();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        await cancellation.CancelAsync();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await RunAsync(_app.FileSystem, processRunner, cancellation.Token);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe("scaffold was stopped");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unexpected_exception_rolls_back_and_is_rethrown()
    {
        var before = _app.TakeSnapshot();
        var processRunner = new FakeProcessRunner((_, _) => throw new InvalidOperationException("bug"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => RunAsync(_app.FileSystem, processRunner));

        var after = _app.TakeSnapshot();

        exception.Message.ShouldBe("bug");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Unexpected_exception_whose_rollback_cannot_restore_a_file_names_the_file()
    {
        var bug = new InvalidOperationException("bug");
        var fileSystem = new FailingFileSystem(_app.Root, failingRestore: SampleApp.ProgramPath);
        var processRunner = new FakeProcessRunner((_, _) => throw bug);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(fileSystem, processRunner));

        var hasModuleFolder = _app.Exists(ModuleFolder);
        var appSettings = _app.Read(SampleApp.AppSettingsPath);

        exception.InnerException.ShouldBeSameAs(bug);
        hasModuleFolder.ShouldBeFalse();
        appSettings.ShouldBe(SampleApp.AppSettings);
        exception.Message.ShouldBe(
            "the rollback did not finish, restore these by hand: "
            + $"cannot restore {SampleApp.ProgramPath}: still read-only");
    }

    [Fact]
    public async Task Rollback_that_cannot_restore_a_file_names_it()
    {
        var fileSystem = new FailingFileSystem(
            _app.Root,
            failingWrite: LastEdit,
            failingRestore: SampleApp.ProgramPath);

        var outcome = await RunAsync(fileSystem, FakeProcessRunner.Succeeding());

        var hasModuleFolder = _app.Exists(ModuleFolder);
        var appSettings = _app.Read(SampleApp.AppSettingsPath);
        var message = ReadMessage(outcome);

        hasModuleFolder.ShouldBeFalse();
        appSettings.ShouldBe(SampleApp.AppSettings);
        message.ShouldBe(
            $"cannot write {SampleApp.AppSettingsPath}: disk full; "
            + "the rollback did not finish, restore these by hand: "
            + $"cannot restore {SampleApp.ProgramPath}: still read-only");
    }

    [Fact]
    public async Task Write_refused_before_it_changed_the_file_needs_no_restore()
    {
        var before = _app.TakeSnapshot();
        var fileSystem = new FailingFileSystem(
            _app.Root,
            failingWrite: LastEdit,
            failingRestore: SampleApp.AppSettingsPath,
            writesPartially: false);

        var outcome = await RunAsync(fileSystem, FakeProcessRunner.Succeeding());

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"cannot write {SampleApp.AppSettingsPath}: disk full; {RolledBack}");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task File_changed_after_planning_stops_the_run_before_any_write()
    {
        var plan = Plan();
        _app.Write(SampleApp.ProgramPath, "changed");
        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await new Transaction(_app.FileSystem, processRunner).RunAsync(plan, CancellationToken);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"{SampleApp.ProgramPath} changed after scaffold read it");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task File_created_after_planning_stops_the_run_before_any_write()
    {
        var plan = Plan();
        _app.Write(ModuleClassPath, "created by hand");
        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await new Transaction(_app.FileSystem, processRunner).RunAsync(plan, CancellationToken);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"{ModuleClassPath} already exists");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Plan_that_edits_one_file_twice_is_refused_before_any_write()
    {
        var program = _app.ReadLayout().Program;
        var plan = new ScaffoldPlan(
            NewFiles,
            [
                PlanEdit(program, "using Acme.Shop.Bootstrapper.Platform;\n", "using Acme.Shop.Modules.Notes;\n"),
                PlanEdit(program, "builder.AddPlatform();\n", "builder.AddModule<NotesModule>();\n"),
            ],
            [MigrationStep]);

        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await new Transaction(_app.FileSystem, processRunner).RunAsync(plan, CancellationToken);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"{SampleApp.ProgramPath} is planned more than once; a plan writes each file once");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Plan_that_creates_and_edits_one_file_is_refused_before_any_write()
    {
        var program = _app.ReadLayout().Program;
        var plan = new ScaffoldPlan(
            [.. NewFiles, new NewFile(SampleApp.ProgramPath, "var app = 1;\n")],
            [PlanEdit(program, "builder.AddPlatform();\n", "builder.AddModule<NotesModule>();\n")],
            [MigrationStep]);

        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await new Transaction(_app.FileSystem, processRunner).RunAsync(plan, CancellationToken);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"{SampleApp.ProgramPath} is planned more than once; a plan writes each file once");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ModuleClassPath)]
    [InlineData("src/Modules/Notes/Acme.Shop.Modules.Notes/notesmodule.cs")]
    public async Task Plan_that_creates_one_file_twice_in_any_letter_case_is_refused_before_any_write(String secondPath)
    {
        var plan = new ScaffoldPlan(
            [.. NewFiles, new NewFile(secondPath, "partial class NotesModule;\n")],
            [],
            [MigrationStep]);

        var before = _app.TakeSnapshot();
        var processRunner = FakeProcessRunner.Succeeding();

        var outcome = await new Transaction(_app.FileSystem, processRunner).RunAsync(plan, CancellationToken);

        var after = _app.TakeSnapshot();
        var message = ReadMessage(outcome);

        message.ShouldBe($"{ModuleClassPath} is planned more than once; a plan writes each file once");
        after.ShouldBe(before);
        processRunner.Steps.ShouldBeEmpty();
    }

    private Task<Result<Unit>> RunAsync(
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        Optional<CancellationToken> cancellationToken = default) =>
        new Transaction(fileSystem, processRunner)
            .RunAsync(Plan(), cancellationToken.GetValueOrDefault(CancellationToken));

    private ScaffoldPlan Plan()
    {
        var app = _app.ReadLayout();

        return new ScaffoldPlan(
            NewFiles,
            [
                PlanEdit(
                    app.Solution,
                    "  <Folder Name=\"/src/Modules/\" />\n",
                    "  <Folder Name=\"/src/Modules/Notes/\" />\n"),
                PlanEdit(
                    app.HostProject,
                    "Infrastructure.csproj\" />\n",
                    "    <ProjectReference Include=\"Notes\" />\n"),
                PlanEdit(app.Program, "builder.AddPlatform();\n", "builder.AddModule<NotesModule>();\n"),
                PlanEdit(app.AppSettings, "\"acme_shop\"", ",\n      \"Notes\": true"),
            ],
            [MigrationStep]);
    }

    private static FileEdit PlanEdit(TextFile file, String anchor, String addition) =>
        file
            .PlanEdit(text => InsertAfter(text, anchor, addition))
            .Value;

    private static Result<String> InsertAfter(String text, String anchor, String addition) =>
        text.Contains(anchor, StringComparison.Ordinal)
            ? text.Replace(anchor, anchor + addition, StringComparison.Ordinal)
            : Error.Failure("test.anchor", $"no {anchor}");

    private static String ReadMessage(Result<Unit> outcome) =>
        outcome.Match(_ => "success", error => error.Message);
}
