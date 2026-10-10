using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Events;

public sealed class EventFinderTests
{
    private const String NotesEvents = "Acme.Shop.Modules.Notes.Contracts.IntegrationEvents";

    private const String TagsEvents = "Acme.Shop.Modules.Tags.Contracts.IntegrationEvents";

    private const String SupportedTypes =
        "supported member types: DateTimeOffset, Guid, String, Int32, Int64, Decimal, Double, Boolean, an array, "
        + "IReadOnlyList<T>, IReadOnlyCollection<T>, an enum, a typed id (ITypedId<T>) or a value object "
        + "(IValueObject<T, TValue>) declared in the event's project, and Optional<T> whose T is one of the first "
        + "eight or declared there";

    private const String NotesDomain =
        """
        namespace Acme.Shop.Modules.Notes.Domain.Notes;

        internal readonly record struct NoteId(Guid Value) : ITypedId<NoteId>;

        internal readonly record struct NoteTitle : IValueObject<NoteTitle, String>;

        internal readonly record struct NoteCode : IValueObject<NoteCode, Guid>;

        internal readonly record struct NoteShapeCode : IValueObject<NoteShapeCode, NoteShape>;

        internal enum NoteKind
        {
            Plain,
            Checklist,
        }

        internal sealed record NoteShape(Int32 Width);
        """;

    private const String NoteCreatedDomainEvent =
        """
        using Tsikhanau.Monolith.Domain.Events;

        namespace Acme.Shop.Modules.Notes.Domain.Notes.Events;

        internal sealed record NoteCreatedDomainEvent(
            DateTimeOffset OccurredOn,
            NoteId NoteId,
            NoteTitle Title,
            NoteKind Kind,
            Optional<NoteTitle> Subtitle,
            Tsikhanau.Railway.Optional<String> Comment,
            IReadOnlyList<String> Labels,
            IReadOnlyCollection<NoteId> LinkedNoteIds,
            String[] Aliases,
            Int32 Revision,
            Int64 Size,
            Decimal Weight,
            Double Score,
            Boolean IsPinned,
            Guid AuthorId,
            String DisplayName,
            NoteCode Code)
            : DomainEvent(OccurredOn);
        """;

    private const String NoteCreatedIntegrationEvent =
        """
        namespace Acme.Shop.Modules.Notes.Contracts.IntegrationEvents;

        [IntegrationEventName("notes.note-created")]
        public sealed record NoteCreatedIntegrationEvent(
            DateTimeOffset OccurredOn,
            Guid NoteId,
            Optional<String> CreatorId,
            NoteAudience Audience)
            : IntegrationEvent(OccurredOn);

        public enum NoteAudience
        {
            Everyone,
            Owner,
        }
        """;

    private const String TagAddedDomainEvent =
        """
        namespace Acme.Shop.Modules.Tags.Domain.Tags.Events;

        internal sealed record TagAddedDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
        """;

    [Fact]
    public void Domain_event_of_the_module_gets_a_sample_for_every_member()
    {
        var found = CreateFinder()
            .Find("Notes", "NoteCreatedDomainEvent");

        var handledEvent = found.GetValueOrDefault(() => throw new InvalidOperationException(ReadMessage(found)));

        handledEvent.Name.ShouldBe("NoteCreatedDomainEvent");
        handledEvent.Namespace.ShouldBe("Acme.Shop.Modules.Notes.Domain.Notes.Events");
        handledEvent.Kind.ShouldBe(EventKind.Domain);
        handledEvent.OwnerModule.ShouldBe("Notes");
        handledEvent.Members
            .Select(member => member.Expression)
            .ShouldBe(
            [
                "App.Time.GetUtcNow()",
                "TypedIdFactory<NoteId>.Create(Guid.CreateVersion7(App.Time.GetUtcNow()))",
                "ValueObjectFactory<NoteTitle, String>.Create(\"title\")",
                "NoteKind.Plain",
                "Optional<NoteTitle>.None()",
                "Optional<String>.None()",
                "[]",
                "[]",
                "[]",
                "1",
                "1",
                "1",
                "1",
                "true",
                "Guid.CreateVersion7(App.Time.GetUtcNow())",
                "\"display-name\"",
                "ValueObjectFactory<NoteCode, Guid>.Create(Guid.CreateVersion7(App.Time.GetUtcNow()))",
            ]);

        handledEvent.Usings.ShouldBe(
            [
                "Acme.Shop.Modules.Notes.Domain.Notes.Events",
                "Acme.Shop.Modules.Notes.Domain.Notes",
                "Tsikhanau.Monolith.Infrastructure.Persistence",
                "Tsikhanau.Railway",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Integration_event_of_a_neighbour_is_found_in_its_contracts()
    {
        var found = CreateFinder()
            .Find("Tags", "NoteCreatedIntegrationEvent");

        var handledEvent = found.GetValueOrDefault(() => throw new InvalidOperationException(ReadMessage(found)));

        handledEvent.Kind.ShouldBe(EventKind.Integration);
        handledEvent.OwnerModule.ShouldBe("Notes");
        handledEvent.Members
            .Select(member => member.Expression)
            .ShouldBe(
            [
                "App.Time.GetUtcNow()",
                "Guid.CreateVersion7(App.Time.GetUtcNow())",
                "Optional<String>.None()",
                "NoteAudience.Everyone",
            ]);

        handledEvent.Usings.ShouldBe([NotesEvents, "Tsikhanau.Railway"], ignoreOrder: true);
    }

    [Fact]
    public void Full_name_tells_two_events_of_one_name_apart()
    {
        var finder = CreateFinder(
            ("Tags", true, NoteCreatedIntegrationEvent.Replace("Notes", "Tags", StringComparison.Ordinal)));

        var ambiguous = finder.Find("Billing", "NoteCreatedIntegrationEvent");
        var named = finder.Find("Billing", $"{TagsEvents}.NoteCreatedIntegrationEvent");

        var message = ReadMessage(ambiguous);
        var owner = named.Map(handledEvent => handledEvent.OwnerModule);

        message.ShouldBe(
            "--event NoteCreatedIntegrationEvent names more than one type: "
            + $"{NotesEvents}.NoteCreatedIntegrationEvent (the Contracts of Notes), "
            + $"{TagsEvents}.NoteCreatedIntegrationEvent (the Contracts of Tags); "
            + "pass the full name");

        owner.ShouldBe(Result.Success("Tags"));
    }

    [Theory]
    [InlineData(
        "Notes",
        "NoteDeletedDomainEvent",
        "--event NoteDeletedDomainEvent: no such type; scaffold looks for a domain event in the module project of "
        + "Notes and for an integration event in the Contracts of the other modules (found: NoteCreatedDomainEvent)")]
    [InlineData(
        "Tags",
        "Acme.Shop.Modules.Notes.Domain.NoteCreatedIntegrationEvent",
        "--event Acme.Shop.Modules.Notes.Domain.NoteCreatedIntegrationEvent: no such type; scaffold looks for a "
        + "domain event in the module project of Tags and for an integration event in the Contracts of the other "
        + "modules (found: NoteCreatedIntegrationEvent, TagAddedDomainEvent)")]
    [InlineData(
        "Notes",
        "NoteCreatedIntegrationEvent",
        $"{NotesEvents}.NoteCreatedIntegrationEvent is an integration event of Notes itself: a module reacts to its "
        + "own changes through the domain event it publishes the integration event from")]
    [InlineData(
        "Notes",
        "TagAddedDomainEvent",
        "Acme.Shop.Modules.Tags.Domain.Tags.Events.TagAddedDomainEvent is a domain event of Tags: a domain event "
        + "never leaves its module; handle the integration event that Tags publishes for it")]
    [InlineData(
        "Notes",
        "NoteShape",
        "Acme.Shop.Modules.Notes.Domain.Notes.NoteShape in the module project of Notes is not an event: it derives "
        + "from neither DomainEvent nor IntegrationEvent")]
    [InlineData(
        "Tags",
        "NoteArchivedDomainEvent",
        "Acme.Shop.Modules.Notes.Contracts.NoteArchivedDomainEvent sits in the Contracts of Notes, where a domain "
        + "event does not belong")]
    [InlineData(
        "Tags",
        "NoteMovedIntegrationEvent",
        "Acme.Shop.Modules.Notes.NoteMovedIntegrationEvent sits in the module project of Notes, where an integration "
        + "event does not belong")]
    public void Event_the_module_cannot_handle_is_refused(String moduleName, String eventType, String message)
    {
        var finder = CreateFinder(
            (
                "Notes",
                true,
                """
                namespace Acme.Shop.Modules.Notes.Contracts;

                public sealed record NoteArchivedDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
                """),
            (
                "Notes",
                false,
                """
                namespace Acme.Shop.Modules.Notes;

                public sealed record NoteMovedIntegrationEvent(DateTimeOffset OccurredOn)
                    : IntegrationEvent(OccurredOn);
                """));

        var found = finder.Find(moduleName, eventType);

        ReadMessage(found)
            .ShouldBe(message);
    }

    [Fact]
    public void Module_with_no_event_to_handle_is_told_that_none_was_found()
    {
        var finder = CreateFinder(("Billing", false, "namespace Acme.Shop.Modules.Billing;\n"));

        var found = finder.Find("Billing", "InvoicePaidDomainEvent");

        ReadMessage(found)
            .ShouldBe(
                "--event InvoicePaidDomainEvent: no such type; scaffold looks for a domain event in the module project "
                + "of Billing and for an integration event in the Contracts of the other modules (found: "
                + "NoteCreatedIntegrationEvent)");
    }

    [Fact]
    public void Every_member_without_a_sample_is_refused_in_one_message()
    {
        var finder = CreateFinder(
            (
                "Notes",
                false,
                """
                namespace Acme.Shop.Modules.Notes.Domain.Notes.Events;

                internal sealed record NoteMovedDomainEvent(
                    DateTimeOffset OccurredOn,
                    String? Folder,
                    NoteShape Shape,
                    Int16 Depth,
                    IEnumerable<String> Steps,
                    Dictionary<String, Int32> Counts,
                    Optional<FolderPath> Path,
                    NoteShapeCode ShapeCode)
                    : DomainEvent(OccurredOn);
                """));

        var found = finder.Find("Notes", "NoteMovedDomainEvent");

        ReadMessage(found)
            .ShouldBe(
                "cannot build a sample of NoteMovedDomainEvent.Folder (String?), "
                + "NoteMovedDomainEvent.Shape (NoteShape), NoteMovedDomainEvent.Depth (Int16), "
                + "NoteMovedDomainEvent.Steps (IEnumerable<String>), "
                + "NoteMovedDomainEvent.Counts (Dictionary<String, Int32>), NoteMovedDomainEvent.Path "
                + "(Optional<FolderPath>), NoteMovedDomainEvent.ShapeCode (NoteShapeCode) for the generated test; "
                + SupportedTypes);
    }

    [Fact]
    public void Event_without_positional_members_is_refused()
    {
        var finder = CreateFinder(
            (
                "Notes",
                false,
                """
                namespace Acme.Shop.Modules.Notes.Domain.Notes.Events;

                internal sealed record NoteSealedDomainEvent : DomainEvent
                {
                    public NoteSealedDomainEvent(DateTimeOffset occurredOn) : base(occurredOn) { }
                }
                """));

        var found = finder.Find("Notes", "NoteSealedDomainEvent");

        ReadMessage(found)
            .ShouldBe(
                "Acme.Shop.Modules.Notes.Domain.Notes.Events.NoteSealedDomainEvent has no positional members; "
                + "scaffold builds the event in the generated test through its primary constructor");
    }

    private static EventFinder CreateFinder(params (String Module, Boolean IsContracts, String Text)[] extraFiles)
    {
        IEnumerable<(String Module, Boolean IsContracts, String Text)> files =
        [
            ("Notes", false, NotesDomain),
            ("Notes", false, NoteCreatedDomainEvent),
            ("Notes", true, NoteCreatedIntegrationEvent),
            ("Tags", false, TagAddedDomainEvent),
            .. extraFiles,
        ];

        return new EventFinder(
        [
            .. files
                .GroupBy(file => (file.Module, file.IsContracts))
                .Select(project => new SourceProject(
                    project.Key.Module,
                    project.Key.IsContracts,
                    [.. project.Select((file, index) => new SourceFile($"{index}.cs", file.Text))])),
        ]);
    }

    private static String ReadMessage(Result<EventDeclaration> found) =>
        found.Match(_ => "found", error => error.Message);
}
