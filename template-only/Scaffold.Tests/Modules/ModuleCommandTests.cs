using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class ModuleCommandTests : IDisposable
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String ModulesFolder = "  <Folder Name=\"/src/Modules/\" />\n";

    private const String CoreReference =
        """    <ProjectReference Include="..\..\Core\Infrastructure\Tsikhanau.Monolith.Infrastructure.csproj" />"""
        + "\n";

    private const String PlatformUsing = "using Acme.Shop.Bootstrapper.Platform;\n";

    private const String PlatformCall = "    builder.AddPlatform();\n";

    private const String ModuleApiUsing = "using Tsikhanau.Monolith.Api.Modules;\n";

    private const String NotesProjectFolder = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String NotesContractsProject =
        "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts/Acme.Shop.Modules.Notes.Contracts.csproj";

    private const String NotesDesignTimeFactory =
        $"{NotesProjectFolder}/Infrastructure/Persistence/NotesDbContextDesignTimeFactory.cs";

    private const String NotesSettings = $"{NotesProjectFolder}/Settings/NotesSettings.cs";

    private const String QueuesEnd = "      \"ClaimBatchSize\": 16\n    }\n";

    private const Int32 FirstNewFile = 1;

    private const Int32 LastNewFile = 7;

    private const Int32 SolutionEdit = 8;

    private const Int32 ProgramEdit = 10;

    private const Int32 SettingsFile = 8;

    private const Int32 AppSettingsEdit = 12;

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
    public async Task Module_is_seven_new_files_three_edits_and_its_first_migration()
    {
        var exitCode = await RunAsync("Notes");

        var status = _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var output = _output.ToString();

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe("scaffold: done; next, run ./gate.sh and commit\n");
        _processRunner.Steps.ShouldBe(["bash scripts/add-migration.sh Notes Initial"]);
        status.ShouldBe(
            [
                $" M {SampleApp.SolutionPath}",
                $" M {SampleApp.HostProjectPath}",
                $" M {SampleApp.ProgramPath}",
                $"?? {NotesContractsProject}",
                $"?? {NotesProjectFolder}/Acme.Shop.Modules.Notes.csproj",
                $"?? {NotesProjectFolder}/NotesModule.cs",
                $"?? {NotesProjectFolder}/INotesModule.cs",
                $"?? {NotesProjectFolder}/Infrastructure/NotesModuleFacade.cs",
                $"?? {NotesProjectFolder}/Infrastructure/Persistence/NotesDbContext.cs",
                $"?? {NotesDesignTimeFactory}",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task First_module_follows_the_modules_folder_the_core_references_and_the_platform_lines()
    {
        var exitCode = await RunAsync("Notes");

        var solution = _app.Read(SampleApp.SolutionPath);
        var hostProject = _app.Read(SampleApp.HostProjectPath);
        var program = _app.Read(SampleApp.ProgramPath);

        exitCode.ShouldBe(0, _error.ToString());
        solution.ShouldBe(Insert(SampleApp.Solution, ModulesFolder, BuildFolder("Notes")));
        hostProject.ShouldBe(Insert(SampleApp.HostProject, CoreReference, BuildReference("Notes")));
        program.ShouldBe(
            Insert(
                Insert(SampleApp.Program, PlatformUsing, BuildUsing("Notes") + ModuleApiUsing),
                PlatformCall,
                BuildCall("Notes")));
    }

    [Theory]
    [InlineData("Notes", "Billing")]
    [InlineData("HTTPLogs", "Hello")]
    public async Task Second_module_that_sorts_before_the_first_goes_before_it_everywhere(String first, String second)
    {
        await RunAsync(first, "--settings");
        _app.Commit();

        var exitCode = await RunAsync(second, "--settings");

        exitCode.ShouldBe(0, _error.ToString());
        ShouldListModulesInOrder(second, first);
    }

    [Theory]
    [InlineData("Notes", "Tags")]
    [InlineData("Notes", "Notes2")]
    public async Task Second_module_that_sorts_after_the_first_goes_after_it_everywhere(String first, String second)
    {
        await RunAsync(first, "--settings");
        _app.Commit();

        var exitCode = await RunAsync(second, "--settings");

        exitCode.ShouldBe(0, _error.ToString());
        ShouldListModulesInOrder(first, second);
    }

    [Theory]
    [InlineData(FirstNewFile, NotesContractsProject)]
    [InlineData(LastNewFile, NotesDesignTimeFactory)]
    [InlineData(SolutionEdit, SampleApp.SolutionPath)]
    [InlineData(ProgramEdit, SampleApp.ProgramPath)]
    public async Task Failed_write_leaves_the_tree_as_it_was(Int32 failingWrite, String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(new FailingFileSystem(_app.Root, failingWrite: failingWrite), "Notes");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
        _processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Failed_migration_deletes_the_module_with_what_it_wrote_and_restores_the_edits()
    {
        var before = _app.TakeSnapshot();
        var processRunner = new FakeProcessRunner((_, _) =>
        {
            _app.Write($"{NotesProjectFolder}/Infrastructure/Migrations/20261010000000_Initial.cs", "migration");
            _app.Write($"{NotesProjectFolder}/obj/project.assets.json", "{}");
            return Task.FromResult(Result.Failure(Error.Failure("step", "the migration failed")));
        });

        var exitCode = await new ScaffoldTool(_app.FileSystem, processRunner, _output, _error)
            .RunAsync(["module", "Notes"], CancellationToken);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();
        var hasModuleFolder = _app.Exists("src/Modules/Notes");

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: the migration failed; {RolledBack}\n");
        after.ShouldBe(before);
        hasModuleFolder.ShouldBeFalse();
    }

    [Fact]
    public async Task Module_with_settings_adds_its_settings_record_and_its_section_in_the_host_settings()
    {
        var exitCode = await RunAsync("Notes", "--settings");

        var status = _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var appSettings = _app.Read(SampleApp.AppSettingsPath);

        exitCode.ShouldBe(0, _error.ToString());
        _processRunner.Steps.ShouldBe(["bash scripts/add-migration.sh Notes Initial"]);
        status.ShouldBe(
            [
                $" M {SampleApp.SolutionPath}",
                $" M {SampleApp.HostProjectPath}",
                $" M {SampleApp.ProgramPath}",
                $" M {SampleApp.AppSettingsPath}",
                $"?? {NotesContractsProject}",
                $"?? {NotesProjectFolder}/Acme.Shop.Modules.Notes.csproj",
                $"?? {NotesProjectFolder}/NotesModule.cs",
                $"?? {NotesProjectFolder}/INotesModule.cs",
                $"?? {NotesProjectFolder}/Infrastructure/NotesModuleFacade.cs",
                $"?? {NotesProjectFolder}/Infrastructure/Persistence/NotesDbContext.cs",
                $"?? {NotesDesignTimeFactory}",
                $"?? {NotesSettings}",
            ],
            ignoreOrder: true);

        appSettings.ShouldBe(
            WithModules(
                """
                    "Modules": {
                      "Notes": {}
                    }
                """));
    }

    [Theory]
    [InlineData(SettingsFile, NotesSettings)]
    [InlineData(AppSettingsEdit, SampleApp.AppSettingsPath)]
    public async Task Failed_write_of_a_module_with_settings_leaves_the_tree_as_it_was(
        Int32 failingWrite,
        String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            "Notes",
            "--settings");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
        _processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Module_whose_settings_section_exists_is_refused_before_anything_is_written()
    {
        _app.Write(SampleApp.AppSettingsPath, WithModules("    \"Modules\": {\n      \"Notes\": {}\n    }"));
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("Notes", "--settings");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: {SampleApp.AppSettingsPath} must hold no App:Modules:Notes yet\n");
        after.ShouldBe(before);
        _processRunner.Steps.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("notes", "Notes")]
    [InlineData("HTTPLog", "HttpLog")]
    public async Task Folder_of_the_module_in_another_letter_case_is_refused(String folder, String name)
    {
        _app.Write($"src/Modules/{folder}/readme.md", folder);
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(name);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: src/Modules/{folder} already exists\n");
        after.ShouldBe(before);
        _processRunner.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Module_the_app_still_lists_is_refused_with_every_place_that_lists_it()
    {
        await RunAsync("Notes");
        Directory.Delete(Path.Combine(_app.Root, "src/Modules/Notes"), recursive: true);
        _app.Commit();
        _error
            .GetStringBuilder()
            .Clear();

        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("Notes");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        after.ShouldBe(before);
        error.ShouldBe(
            $"scaffold: {SampleApp.SolutionPath} must hold no folder /src/Modules/Notes/ yet; "
            + $"{SampleApp.HostProjectPath} must hold no reference to "
            + @"..\..\Modules\Notes\Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj yet; "
            + $"{SampleApp.ProgramPath} must hold no builder.AddModule<NotesModule>(); yet\n");
    }

    [Fact]
    public async Task Invalid_name_is_refused_before_anything_is_written()
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("notes");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        after.ShouldBe(before);
        error.ShouldBe(
            "scaffold: module name 'notes' must be PascalCase: an ASCII capital letter, then letters and digits\n");
    }

    [Fact]
    public async Task Name_that_makes_a_line_longer_than_120_characters_is_refused()
    {
        _app.Write(
            SampleApp.ProgramPath,
            SampleApp.Program.Replace(
                PlatformUsing,
                "using Contoso.Enterprise.Logistics.Warehousing.Bootstrapper.Platform;\n",
                StringComparison.Ordinal));

        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("InventoryReservationAdjustmentApprovals");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        after.ShouldBe(before);
        error.ShouldBe(
            "scaffold: module name 'InventoryReservationAdjustmentApprovals' makes "
            + "src/Modules/InventoryReservationAdjustmentApprovals/"
            + "Acme.Shop.Modules.InventoryReservationAdjustmentApprovals/"
            + "InventoryReservationAdjustmentApprovalsModule.cs:2 122 characters long, "
            + "more than the 120 the build allows; choose a shorter name\n");
    }

    private Task<Int32> RunAsync(params String[] moduleArguments) => RunAsync(_app.FileSystem, moduleArguments);

    private Task<Int32> RunAsync(IFileSystem fileSystem, params String[] moduleArguments) =>
        new ScaffoldTool(fileSystem, _processRunner, _output, _error)
            .RunAsync(["module", .. moduleArguments], CancellationToken);

    private static String WithModules(String modules) =>
        SampleApp.AppSettings.Replace(
            QueuesEnd,
            $"      \"ClaimBatchSize\": 16\n    }},\n{modules}\n",
            StringComparison.Ordinal);

    private void ShouldListModulesInOrder(String earlier, String later)
    {
        var solution = _app.Read(SampleApp.SolutionPath);
        var hostProject = _app.Read(SampleApp.HostProjectPath);
        var program = _app.Read(SampleApp.ProgramPath);
        var appSettings = _app.Read(SampleApp.AppSettingsPath);

        _app.ShouldSatisfyAllConditions(
            () => solution.ShouldBe(
                Insert(SampleApp.Solution, ModulesFolder, BuildFolder(earlier) + BuildFolder(later))),
            () => hostProject.ShouldBe(
                Insert(SampleApp.HostProject, CoreReference, BuildReference(earlier) + BuildReference(later))),
            () => program.ShouldBe(
                Insert(
                    Insert(
                        SampleApp.Program,
                        PlatformUsing,
                        BuildUsing(earlier) + BuildUsing(later) + ModuleApiUsing),
                    PlatformCall,
                    BuildCall(earlier) + BuildCall(later))),
            () => appSettings.ShouldBe(
                WithModules($"    \"Modules\": {{\n      \"{earlier}\": {{}},\n      \"{later}\": {{}}\n    }}")));
    }

    private static String Insert(String text, String anchor, String addition) =>
        text.Replace(anchor, anchor + addition, StringComparison.Ordinal);

    private static String BuildFolder(String name)
    {
        var projectFolder = $"src/Modules/{name}/Acme.Shop.Modules.{name}";

        return $"""
              <Folder Name="/src/Modules/{name}/">
                <Project Path="{projectFolder}.Contracts/Acme.Shop.Modules.{name}.Contracts.csproj" />
                <Project Path="{projectFolder}/Acme.Shop.Modules.{name}.csproj" />
              </Folder>

            """;
    }

    private static String BuildReference(String name)
    {
        var project = $"Acme.Shop.Modules.{name}";

        return $@"    <ProjectReference Include=""..\..\Modules\{name}\{project}\{project}.csproj"" />" + "\n";
    }

    private static String BuildUsing(String name) => $"using Acme.Shop.Modules.{name};\n";

    private static String BuildCall(String name) => $"    builder.AddModule<{name}Module>();\n";
}
