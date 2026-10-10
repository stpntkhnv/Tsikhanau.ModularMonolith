using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Tests.Support;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class EventHandlerCommandTests : IAsyncLifetime
{
    private const String RolledBack = "files scaffold created are deleted, files it changed are back as they were";

    private const String NotesProject = "src/Modules/Notes/Acme.Shop.Modules.Notes";

    private const String NotesContracts = "src/Modules/Notes/Acme.Shop.Modules.Notes.Contracts";

    private const String TagsProject = "src/Modules/Tags/Acme.Shop.Modules.Tags";

    private const String TagsContracts = "src/Modules/Tags/Acme.Shop.Modules.Tags.Contracts";

    private const String TagsProjectFile = $"{TagsProject}/Acme.Shop.Modules.Tags.csproj";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String TagsTests = "src/Tests/IntegrationTests/Modules/Tags";

    private const String OwnContractsReference =
        @"    <ProjectReference Include=""..\Acme.Shop.Modules.Tags.Contracts\"
        + @"Acme.Shop.Modules.Tags.Contracts.csproj"" />";

    private const String NotesContractsReference =
        @"    <ProjectReference Include=""..\..\Notes\Acme.Shop.Modules.Notes.Contracts\"
        + @"Acme.Shop.Modules.Notes.Contracts.csproj"" />";

    private const String NotesDomainTypes =
        """
        namespace Acme.Shop.Modules.Notes.Domain.Notes;

        internal readonly record struct NoteId(Guid Value) : ITypedId<NoteId>;
        """;

    private const String NoteCreatedDomainEvent =
        """
        using Tsikhanau.Monolith.Domain.Events;

        namespace Acme.Shop.Modules.Notes.Domain.Notes.Events;

        internal sealed record NoteCreatedDomainEvent(DateTimeOffset OccurredOn, NoteId NoteId)
            : DomainEvent(OccurredOn);
        """;

    private const String NoteCreatedIntegrationEvent =
        """
        using Tsikhanau.Monolith.Contracts.IntegrationEvents;

        namespace Acme.Shop.Modules.Notes.Contracts.IntegrationEvents;

        [IntegrationEventName("notes.note-created")]
        public sealed record NoteCreatedIntegrationEvent(DateTimeOffset OccurredOn, Guid NoteId, String Title)
            : IntegrationEvent(OccurredOn);
        """;

    private const String TagAddedIntegrationEvent =
        """
        namespace Acme.Shop.Modules.Tags.Contracts.IntegrationEvents;

        [IntegrationEventName("tags.tag-added")]
        public sealed record TagAddedIntegrationEvent(DateTimeOffset OccurredOn) : IntegrationEvent(OccurredOn);
        """;

    private const String NeighbourEventCommand =
        "feature Tags NoteCreated --kind event-handler --event NoteCreatedIntegrationEvent";

    private const String DomainEventCommand =
        "feature Notes NoteCreated --kind event-handler --event NoteCreatedDomainEvent";

    private readonly SampleApp _app = SampleApp.Create();

    private readonly StringWriter _output = new();

    private readonly StringWriter _error = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await AddModuleAsync("Notes");
        await AddModuleAsync("Tags");
        _app.Write($"{NotesProject}/Domain/Notes/NoteId.cs", NotesDomainTypes);
        _app.Write($"{NotesProject}/Domain/Notes/Events/NoteCreatedDomainEvent.cs", NoteCreatedDomainEvent);
        _app.Write($"{NotesContracts}/IntegrationEvents/NoteCreatedIntegrationEvent.cs", NoteCreatedIntegrationEvent);
        _app.Commit();
    }

    public ValueTask DisposeAsync()
    {
        _output.Dispose();
        _error.Dispose();
        _app.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handler_of_a_domain_event_of_the_module_writes_only_the_handler_and_its_test()
    {
        var exitCode = await RunAsync(DomainEventCommand);

        var status = ReadStatus();
        var output = _output.ToString();

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            "scaffold: done; the new test NoteCreatedTests.Note_created_domain_event_is_handled fails with "
            + "\"NoteCreated in Notes is not implemented\" until the handler does its work; next, run ./gate.sh\n");

        status.ShouldBe(
            [
                $"?? {NotesProject}/Features/NoteCreated/NoteCreatedDomainEventHandler.cs",
                $"?? {NotesTests}/NoteCreatedTests.cs",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Handler_of_an_integration_event_of_a_neighbour_adds_the_reference_to_its_contracts()
    {
        var exitCode = await RunAsync(NeighbourEventCommand);

        var status = ReadStatus();
        var output = _output.ToString();
        var tagsProject = _app.Read(TagsProjectFile);

        exitCode.ShouldBe(0, _error.ToString());
        output.ShouldBe(
            "scaffold: done; the new test NoteCreatedTests.Note_created_integration_event_is_handled_through_the_inbox "
            + "fails with \"NoteCreated in Tags is not implemented\" until the handler does its work; "
            + "next, run ./gate.sh\n");

        status.ShouldBe(
            [
                $" M {TagsProjectFile}",
                $"?? {TagsProject}/Features/NoteCreated/NoteCreatedIntegrationEventHandler.cs",
                $"?? {TagsTests}/NoteCreatedTests.cs",
            ],
            ignoreOrder: true);

        tagsProject.ShouldBe(
            ScaffoldedTagsProject.Replace(
                OwnContractsReference,
                $"{OwnContractsReference}\n{NotesContractsReference}",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Second_handler_of_the_neighbour_event_leaves_the_project_alone()
    {
        await RunAndCommitAsync(NeighbourEventCommand);

        var exitCode = await RunAsync(
            "feature Tags NoteArchived --kind event-handler --event NoteCreatedIntegrationEvent");

        var changedFiles = ReadStatus()
            .Where(line => line.StartsWith(" M", StringComparison.Ordinal));

        exitCode.ShouldBe(0, _error.ToString());
        changedFiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reference_to_the_neighbour_contracts_written_another_way_counts_as_there()
    {
        ReplaceInTagsProject(
            OwnContractsReference,
            $"{OwnContractsReference}\n    <ProjectReference Include=\"../../Notes/Acme.Shop.Modules.Notes.Contracts/"
            + "Acme.Shop.Modules.Notes.Contracts.csproj\"/>");

        var exitCode = await RunAsync(NeighbourEventCommand);

        var changedFiles = ReadStatus()
            .Where(line => line.StartsWith(" M", StringComparison.Ordinal));

        exitCode.ShouldBe(0, _error.ToString());
        changedFiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handler_that_would_close_a_cycle_of_modules_is_refused_before_any_write()
    {
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

    [Theory]
    [InlineData(
        "feature Notes NoteCreated --kind event-handler --event NoteCreatedIntegrationEvent",
        "Acme.Shop.Modules.Notes.Contracts.IntegrationEvents.NoteCreatedIntegrationEvent is an integration event of "
        + "Notes itself: a module reacts to its own changes through the domain event it publishes the integration "
        + "event from")]
    [InlineData(
        "feature Tags NoteCreated --kind event-handler --event NoteCreatedDomainEvent",
        "Acme.Shop.Modules.Notes.Domain.Notes.Events.NoteCreatedDomainEvent is a domain event of Notes: a domain "
        + "event never leaves its module; handle the integration event that Notes publishes for it")]
    [InlineData(
        "feature Tags NoteCreated --kind event-handler --event NoteDeletedIntegrationEvent",
        "--event NoteDeletedIntegrationEvent: no such type; scaffold looks for a domain event in the module project "
        + "of Tags and for an integration event in the Contracts of the other modules "
        + "(found: NoteCreatedIntegrationEvent)")]
    [InlineData(
        "feature Tags NoteCreated --kind event-handler --event NoteCreatedIntegrationEvent --endpoint post",
        "an event handler has no endpoint: the module runs it when the event arrives")]
    [InlineData(
        "feature Tags NoteCreated --kind event-handler",
        "--kind event-handler needs --event <Type>: a domain event of Tags or an integration event of a neighbour")]
    [InlineData(
        "feature Tags NoteCreated --kind command --event NoteCreatedIntegrationEvent",
        "--event belongs to --kind event-handler: a command handles no event")]
    public async Task Event_scaffold_cannot_handle_is_refused_before_any_write(String commandLine, String message)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(commandLine);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: {message}\n");
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Nested_event_is_refused_before_any_write()
    {
        _app.Write(
            $"{NotesProject}/Domain/NoteEvents.cs",
            """
            namespace Acme.Shop.Modules.Notes.Domain;

            public static class NoteEvents
            {
                public sealed record NoteSealedDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
            }
            """);

        _app.Commit();
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync("feature Notes NoteSealed --kind event-handler --event NoteSealedDomainEvent");

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: Acme.Shop.Modules.Notes.Domain.NoteEvents.NoteSealedDomainEvent is nested in NoteEvents; "
            + "scaffold handles only top-level types, one per file (STYLE 7)\n");

        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    public async Task Sources_in_build_output_folders_are_not_read(String buildFolder)
    {
        _app.Write($"{NotesProject}/{buildFolder}/Release/NoteCreatedDomainEvent.cs", NoteCreatedDomainEvent);
        _app.Write(
            $"{NotesProject}/Domain/{buildFolder}/NoteCreatedDomainEvent.cs",
            NoteCreatedDomainEvent.Replace("Events;", "Events.Old;", StringComparison.Ordinal));

        var exitCode = await RunAsync(DomainEventCommand);

        exitCode.ShouldBe(0, _error.ToString());
    }

    [Fact]
    public async Task Event_declared_twice_in_the_sources_is_ambiguous()
    {
        _app.Write(
            $"{NotesProject}/Domain/Archive/NoteCreatedDomainEvent.cs",
            NoteCreatedDomainEvent.Replace("Events;", "Events.Old;", StringComparison.Ordinal));

        var exitCode = await RunAsync(DomainEventCommand);

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: --event NoteCreatedDomainEvent names more than one type: "
            + "Acme.Shop.Modules.Notes.Domain.Notes.Events.NoteCreatedDomainEvent (the module project of Notes), "
            + "Acme.Shop.Modules.Notes.Domain.Notes.Events.Old.NoteCreatedDomainEvent (the module project of Notes); "
            + "pass the full name\n");
    }

    [Fact]
    public async Task Module_project_without_the_reference_to_its_contracts_is_refused()
    {
        ReplaceInTagsProject($"{OwnContractsReference}\n", String.Empty);

        var exitCode = await RunAsync(NeighbourEventCommand);

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            $@"scaffold: {TagsProjectFile} must hold the reference to ..\Acme.Shop.Modules.Tags.Contracts\"
            + "Acme.Shop.Modules.Tags.Contracts.csproj\n");
    }

    [Fact]
    public async Task Line_too_long_because_of_the_event_is_refused_with_the_feature_and_the_event()
    {
        const String LongId = "InventoryReservationAdjustmentLineIdentifierOfTheQuarter";
        _app.Write(
            $"{NotesProject}/Domain/Notes/NoteId.cs",
            NotesDomainTypes.Replace("NoteId", LongId, StringComparison.Ordinal));

        _app.Write(
            $"{NotesProject}/Domain/Notes/Events/NoteCreatedDomainEvent.cs",
            NoteCreatedDomainEvent.Replace("NoteId NoteId", $"{LongId} LineId", StringComparison.Ordinal));

        var exitCode = await RunAsync(DomainEventCommand);

        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe(
            "scaffold: feature name 'NoteCreated' with event NoteCreatedDomainEvent makes "
            + $"{NotesTests}/NoteCreatedTests.cs:17 136 characters long, more than the 120 the build allows; "
            + "choose a shorter name\n");
    }

    [Theory]
    [InlineData(1, $"{TagsProject}/Features/NoteCreated/NoteCreatedIntegrationEventHandler.cs")]
    [InlineData(2, $"{TagsTests}/NoteCreatedTests.cs")]
    [InlineData(3, TagsProjectFile)]
    public async Task Failed_write_of_a_neighbour_event_handler_leaves_the_tree_as_it_was(
        Int32 failingWrite,
        String failingPath)
    {
        var before = _app.TakeSnapshot();

        var exitCode = await RunAsync(
            new FailingFileSystem(_app.Root, failingWrite: failingWrite),
            NeighbourEventCommand);

        var after = _app.TakeSnapshot();
        var error = _error.ToString();

        exitCode.ShouldBe(1);
        error.ShouldBe($"scaffold: cannot write {failingPath}: disk full; {RolledBack}\n");
        after.ShouldBe(before);
    }

    private static String ScaffoldedTagsProject
    {
        get
        {
            var tags = SampleModules.Create("Tags");

            return new ModuleTemplate(tags, hasSettings: false)
                .CreateFiles()
                .Single(newFile => newFile.Path == tags.ProjectPath)
                .Content;
        }
    }

    private void ReplaceInTagsProject(String text, String replacement)
    {
        var tagsProject = _app.Read(TagsProjectFile);

        _app.Write(TagsProjectFile, tagsProject.Replace(text, replacement, StringComparison.Ordinal));
        _app.Commit();
    }

    private async Task AddModuleAsync(String name)
    {
        var exitCode = await new ScaffoldTool(
                _app.FileSystem,
                FakeProcessRunner.Succeeding(),
                TextWriter.Null,
                TextWriter.Null)
            .RunAsync(["module", name], CancellationToken);

        exitCode.ShouldBe(0);
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

    private Task<Int32> RunAsync(String commandLine) => RunAsync(_app.FileSystem, commandLine);

    private Task<Int32> RunAsync(IFileSystem fileSystem, String commandLine) =>
        new ScaffoldTool(fileSystem, FakeProcessRunner.Succeeding(), _output, _error)
            .RunAsync(commandLine.Split(' '), CancellationToken);

    private String[] ReadStatus() =>
        _app
            .TakeSnapshot()
            .Status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
