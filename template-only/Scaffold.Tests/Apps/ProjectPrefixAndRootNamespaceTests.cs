using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Tests.Fakes;
using Tsikhanau.Monolith.Scaffold.Tests.Samples;

namespace Tsikhanau.Monolith.Scaffold.Tests.Apps;

public sealed class ProjectPrefixAndRootNamespaceTests : IDisposable
{
    private const String RootNamespace = "AcmeShop";

    private const String NotesProject = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String NotesContracts = "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts";

    private const String TagsProject = "src/Modules/Tags/Acme.Shop.Modules.Tags";

    private const String TagsContracts = "src/Modules/Tags/Acme.Shop.Modules.Tags.Contracts";

    private const String TagsProjectFile = $"{TagsProject}/Acme.Shop.Modules.Tags.csproj";

    private const String TagsTests = "src/Tests/IntegrationTests/Modules/Tags";

    private const String NotesProjectReference =
        @"<ProjectReference Include=""..\..\Modules\Notes\Acme.Shop.Modules.Notes\"
        + @"Acme.Shop.Modules.Notes.csproj"" />";

    private const String NotesOwnContractsReference =
        @"<ProjectReference Include=""..\Acme.Shop.Modules.Notes.Contracts\"
        + @"Acme.Shop.Modules.Notes.Contracts.csproj"" />";

    private const String TagsOwnContractsReference =
        @"    <ProjectReference Include=""..\Acme.Shop.Modules.Tags.Contracts\"
        + @"Acme.Shop.Modules.Tags.Contracts.csproj"" />";

    private const String NotesContractsReference =
        @"    <ProjectReference Include=""..\..\Notes\Acme.Shop.Modules.Notes.Contracts\"
        + @"Acme.Shop.Modules.Notes.Contracts.csproj"" />";

    private const String NoteCreatedIntegrationEvent =
        """
        using Tsikhanau.Monolith.Contracts.IntegrationEvents;

        namespace AcmeShop.Modules.Notes.Contracts.IntegrationEvents;

        [IntegrationEventName("notes.note-created")]
        public sealed record NoteCreatedIntegrationEvent(DateTimeOffset OccurredOn, Guid NoteId)
            : IntegrationEvent(OccurredOn);
        """;

    private const String TagAddedIntegrationEvent =
        """
        namespace AcmeShop.Modules.Tags.Contracts.IntegrationEvents;

        [IntegrationEventName("tags.tag-added")]
        public sealed record TagAddedIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
        """;

    private const String NeighbourEventCommand =
        "feature Tags NoteCreated --kind event-handler --event NoteCreatedIntegrationEvent";

    private readonly SampleApp _app = SampleApp.Create();

    private readonly StringWriter _output = new();

    private readonly StringWriter _error = new();

    public ProjectPrefixAndRootNamespaceTests()
    {
        _app.Write(SampleApp.ProgramPath, SampleModules.RenameApp(FrozenPrograms.Core102, RootNamespace));
        _app.Commit();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task Module_takes_paths_and_references_from_the_project_prefix_and_namespaces_from_the_root()
    {
        var exitCode = await RunAsync("module Notes");

        var status = ReadStatus();
        var solution = _app.Read(SampleApp.SolutionPath);
        var hostProject = _app.Read(SampleApp.HostProjectPath);
        var program = _app.Read(SampleApp.ProgramPath);
        var notesProject = _app.Read($"{NotesProject}/Acme.Shop.Modules.Notes.csproj");
        var notesModule = _app.Read($"{NotesProject}/NotesModule.cs");

        exitCode.ShouldBe(0, _error.ToString());
        status.ShouldBe(
            [
                $" M {SampleApp.SolutionPath}",
                $" M {SampleApp.HostProjectPath}",
                $" M {SampleApp.ProgramPath}",
                $"?? {NotesContracts}/Acme.Shop.Modules.Notes.Contracts.csproj",
                $"?? {NotesProject}/Acme.Shop.Modules.Notes.csproj",
                $"?? {NotesProject}/NotesModule.cs",
                $"?? {NotesProject}/INotesModule.cs",
                $"?? {NotesProject}/Infrastructure/NotesModuleFacade.cs",
                $"?? {NotesProject}/Infrastructure/Persistence/NotesDbContext.cs",
                $"?? {NotesProject}/Infrastructure/Persistence/NotesDbContextDesignTimeFactory.cs",
            ],
            ignoreOrder: true);

        _app.ShouldSatisfyAllConditions(
            () => solution.ShouldContain(
                $"<Project Path=\"{NotesContracts}/Acme.Shop.Modules.Notes.Contracts.csproj\" />",
                Case.Sensitive),
            () => solution.ShouldContain(
                $"<Project Path=\"{NotesProject}/Acme.Shop.Modules.Notes.csproj\" />",
                Case.Sensitive),
            () => hostProject.ShouldContain(NotesProjectReference, Case.Sensitive),
            () => notesProject.ShouldContain(NotesOwnContractsReference, Case.Sensitive),
            () => notesProject.ShouldContain(
                "<InternalsVisibleTo Include=\"Acme.Shop.IntegrationTests\" />",
                Case.Sensitive),
            () => program.ShouldContain("using AcmeShop.Modules.Notes;\n", Case.Sensitive),
            () => notesModule.ShouldContain("namespace AcmeShop.Modules.Notes;\n", Case.Sensitive));
    }

    [Fact]
    public async Task Neighbour_event_handler_finds_the_modules_and_the_event_through_the_project_prefix()
    {
        await AddNotesTagsAndNoteCreatedAsync();

        var exitCode = await RunAsync(NeighbourEventCommand);

        var status = ReadStatus();
        var tagsProject = _app.Read(TagsProjectFile);
        var handler = _app.Read($"{TagsProject}/Features/NoteCreated/NoteCreatedIntegrationEventHandler.cs");
        var test = _app.Read($"{TagsTests}/NoteCreatedTests.cs");

        exitCode.ShouldBe(0, _error.ToString());
        status.ShouldBe(
            [
                $" M {TagsProjectFile}",
                $"?? {TagsProject}/Features/NoteCreated/NoteCreatedIntegrationEventHandler.cs",
                $"?? {TagsTests}/NoteCreatedTests.cs",
            ],
            ignoreOrder: true);

        _app.ShouldSatisfyAllConditions(
            () => tagsProject.ShouldContain(
                $"{TagsOwnContractsReference}\n{NotesContractsReference}\n",
                Case.Sensitive),
            () => handler.ShouldContain("using AcmeShop.Modules.Notes.Contracts.IntegrationEvents;\n", Case.Sensitive),
            () => handler.ShouldContain("namespace AcmeShop.Modules.Tags.Features.NoteCreated;\n", Case.Sensitive),
            () => test.ShouldContain("namespace AcmeShop.IntegrationTests.Modules.Tags;\n", Case.Sensitive));
    }

    [Fact]
    public async Task Cycle_of_modules_is_found_through_the_projects_the_prefix_names()
    {
        await AddNotesTagsAndNoteCreatedAsync();
        await RunAndCommitAsync(NeighbourEventCommand);
        _app.Write($"{TagsContracts}/IntegrationEvents/TagAddedIntegrationEvent.cs", TagAddedIntegrationEvent);
        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes TagAdded --kind event-handler --event TagAddedIntegrationEvent");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: Notes would reference the Contracts of Tags, which already reaches Notes (Tags -> Notes); "
            + "modules must not reference each other in a cycle\n");

        after.ShouldBe(before);
    }

    [Fact]
    public async Task Route_the_module_maps_is_found_through_the_project_the_prefix_names()
    {
        await RunAndCommitAsync("module Notes");
        await RunAndCommitAsync("feature Notes NoteSummary --kind query --endpoint get");
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes GetNoteSummary --kind query --endpoint get");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: feature GetNoteSummary of Notes would answer GET /notes/note-summary, which "
            + $"{NotesProject}/Features/NoteSummary/NoteSummaryEndpoint.cs:12 maps already; "
            + "choose another feature name\n");

        after.ShouldBe(before);
    }

    private async Task AddNotesTagsAndNoteCreatedAsync()
    {
        await RunAndCommitAsync("module Notes");
        await RunAndCommitAsync("module Tags");
        _app.Write($"{NotesContracts}/IntegrationEvents/NoteCreatedIntegrationEvent.cs", NoteCreatedIntegrationEvent);
        _app.Commit();
    }

    private async Task RunAndCommitAsync(String commandLine)
    {
        var exitCode = await RunAsync(commandLine);

        exitCode.ShouldBe(0, _error.ToString());
        _app.Commit();
        _output
            .GetStringBuilder()
            .Clear();
    }

    private Task<Int32> RunAsync(String commandLine) =>
        new ScaffoldTool(_app.FileSystem, FakeProcessRunner.Succeeding(), _output, _error)
            .RunAsync(commandLine.Split(' '), CancellationToken);

    private String[] ReadStatus() =>
        _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
