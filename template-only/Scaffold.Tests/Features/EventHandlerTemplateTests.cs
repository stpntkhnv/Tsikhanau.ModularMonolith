using Tsikhanau.Monolith.Scaffold.Events;
using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class EventHandlerTemplateTests
{
    private const String NotesFeatures = "src/Modules/Notes/Acme.Shop.Modules.Notes/Features";

    private const String TagsFeatures = "src/Modules/Tags/Acme.Shop.Modules.Tags/Features";

    private const String NotesDomain = "Acme.Shop.Modules.Notes.Domain.Notes";

    private const String NotesEvents = "Acme.Shop.Modules.Notes.Contracts.IntegrationEvents";

    private const String Persistence = "Tsikhanau.Monolith.Infrastructure.Persistence";

    private const String Railway = "Tsikhanau.Railway";

    private const String Clock = "App.Time.GetUtcNow()";

    private const String LongModule = "InventoryReservationAdjustments";

    private const String MaxModule = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const String LongEvent = "ReservationAdjustmentTotalsRecalculated";

    private static readonly EventDeclaration NoteCreatedDomainEvent = new(
        "NoteCreatedDomainEvent",
        $"{NotesDomain}.Events",
        EventKind.Domain,
        "Notes",
        [
            MemberSample.Of(Clock),
            MemberSample.Of($"TypedIdFactory<NoteId>.Create(Guid.CreateVersion7({Clock}))", Persistence, NotesDomain),
            MemberSample.Of("ValueObjectFactory<NoteTitle, String>.Create(\"title\")", Persistence, NotesDomain),
            MemberSample.Of("NoteKind.Plain", NotesDomain),
            MemberSample.Of("Optional<NoteTitle>.None()", Railway, NotesDomain),
            MemberSample.Of("[]"),
            MemberSample.Of("1"),
            MemberSample.Of("true"),
        ]);

    private static readonly EventDeclaration NoteCreatedIntegrationEvent = new(
        "NoteCreatedIntegrationEvent",
        NotesEvents,
        EventKind.Integration,
        "Notes",
        [
            MemberSample.Of(Clock),
            MemberSample.Of($"Guid.CreateVersion7({Clock})"),
            MemberSample.Of("\"title\""),
            MemberSample.Of("Optional<String>.None()", Railway),
        ]);

    [Fact]
    public void Handler_of_a_domain_event_of_the_module_answers_not_implemented()
    {
        var feature = CreateFeature("Notes", "NoteCreated", NoteCreatedDomainEvent);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{NotesFeatures}/NoteCreated/NoteCreatedDomainEventHandler.cs",
                """
                using Acme.Shop.Modules.Notes.Domain.Notes.Events;
                using Tsikhanau.Monolith.Application.Events;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.Notes.Features.NoteCreated;

                internal sealed class NoteCreatedDomainEventHandler : IDomainEventHandler<NoteCreatedDomainEvent>
                {
                    private static readonly Error NotImplemented =
                        Error.Failure("NoteCreated.NotImplemented", "NoteCreated in Notes is not implemented");

                    public Task<Result<Unit>> Handle(NoteCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
                        Task.FromResult(Result.Failure(NotImplemented));
                }

                """),
        ]);
    }

    [Fact]
    public void Handler_of_an_integration_event_of_a_neighbour_answers_not_implemented()
    {
        var feature = CreateFeature("Tags", "NoteCreated", NoteCreatedIntegrationEvent);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{TagsFeatures}/NoteCreated/NoteCreatedIntegrationEventHandler.cs",
                """
                using Acme.Shop.Modules.Notes.Contracts.IntegrationEvents;
                using Tsikhanau.Monolith.Application.Events;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.Tags.Features.NoteCreated;

                internal sealed class NoteCreatedIntegrationEventHandler : IIntegrationEventHandler<NoteCreatedIntegrationEvent>
                {
                    private static readonly Error NotImplemented =
                        Error.Failure("NoteCreated.NotImplemented", "NoteCreated in Tags is not implemented");

                    public Task<Result<Unit>> Handle(
                        NoteCreatedIntegrationEvent integrationEvent,
                        CancellationToken cancellationToken) =>
                        Task.FromResult(Result.Failure(NotImplemented));
                }

                """),
        ]);
    }

    [Fact]
    public void Test_of_a_domain_event_dispatches_it_runs_the_queues_and_compares_the_result_with_success()
    {
        var feature = CreateFeature("Notes", "NoteCreated", NoteCreatedDomainEvent);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                "src/Tests/IntegrationTests/Modules/Notes/NoteCreatedTests.cs",
                """
                using Acme.Shop.Modules.Notes;
                using Acme.Shop.Modules.Notes.Domain.Notes;
                using Acme.Shop.Modules.Notes.Domain.Notes.Events;
                using Acme.Shop.Testing.App;
                using Tsikhanau.Monolith.Infrastructure.Persistence;
                using Tsikhanau.Railway;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class NoteCreatedTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    [Fact]
                    public async Task Note_created_domain_event_is_handled()
                    {
                        var domainEvent = new NoteCreatedDomainEvent(
                            App.Time.GetUtcNow(),
                            TypedIdFactory<NoteId>.Create(Guid.CreateVersion7(App.Time.GetUtcNow())),
                            ValueObjectFactory<NoteTitle, String>.Create("title"),
                            NoteKind.Plain,
                            Optional<NoteTitle>.None(),
                            [],
                            1,
                            true);

                        var dispatched = await App.DispatchAsync<INotesModule>(domainEvent);
                        await App.ProcessQueuesAsync();

                        dispatched.ShouldBe(Result.Success());
                    }
                }

                """));
    }

    [Fact]
    public void Test_of_an_integration_event_publishes_it_runs_the_queues_and_checks_that_its_inbox_row_is_processed()
    {
        var feature = CreateFeature("Tags", "NoteCreated", NoteCreatedIntegrationEvent);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                "src/Tests/IntegrationTests/Modules/Tags/NoteCreatedTests.cs",
                """
                using Acme.Shop.Modules.Notes.Contracts.IntegrationEvents;
                using Acme.Shop.Modules.Tags.Infrastructure.Persistence;
                using Acme.Shop.Testing.App;
                using Tsikhanau.Monolith.Infrastructure.Inbox;
                using Tsikhanau.Railway;

                namespace Acme.Shop.IntegrationTests.Modules.Tags;

                public sealed class NoteCreatedTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    [Fact]
                    public async Task Note_created_integration_event_is_handled_through_the_inbox()
                    {
                        var integrationEvent = new NoteCreatedIntegrationEvent(
                            App.Time.GetUtcNow(),
                            Guid.CreateVersion7(App.Time.GetUtcNow()),
                            "title",
                            Optional<String>.None());

                        await App.PublishAsync(integrationEvent);
                        await App.ProcessQueuesAsync();
                        var inboxRows = await App.Database.ReadQueueRowsAsync(TagsDbContext.SchemaName, InboxMessage.Table);

                        inboxRows
                            .Select(inboxRow => (inboxRow.Id, inboxRow.IsProcessed))
                            .ShouldBe([(integrationEvent.Id, true)]);
                    }
                }

                """));
    }

    [Fact]
    public void Event_of_few_members_is_built_on_one_line_and_its_test_names_no_using_twice()
    {
        var rootEvent = new EventDeclaration(
            "NoteSealedDomainEvent",
            "Acme.Shop.Modules.Notes",
            EventKind.Domain,
            "Notes",
            [MemberSample.Of(Clock)]);

        var feature = CreateFeature("Notes", "NoteSealed", rootEvent);

        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        test.ShouldStartWith(
            """
            using Acme.Shop.Modules.Notes;
            using Acme.Shop.Testing.App;
            using Tsikhanau.Railway;

            """,
            Case.Sensitive);

        test.ShouldContain(
            """
                    var domainEvent = new NoteSealedDomainEvent(App.Time.GetUtcNow());

                    var dispatched = await App.DispatchAsync<INotesModule>(domainEvent);
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Handler_of_an_event_in_an_enclosing_namespace_needs_no_using_for_it()
    {
        var rootEvent = new EventDeclaration(
            "NoteSealedDomainEvent",
            "Acme.Shop.Modules.Notes",
            EventKind.Domain,
            "Notes",
            [MemberSample.Of(Clock)]);

        var feature = CreateFeature("Notes", "NoteSealed", rootEvent);

        var handler = new FeatureTemplate(feature)
            .CreateFiles()
            .Single()
            .Content;

        handler.ShouldStartWith(
            """
            using Tsikhanau.Monolith.Application.Events;
            using Tsikhanau.Railway;

            namespace Acme.Shop.Modules.Notes.Features.NoteSealed;

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Long_names_wrap_the_handler_the_dispatch_and_the_inbox_read()
    {
        var domainEvent = CreateEvent(MaxModule, $"{LongEvent}DomainEvent", EventKind.Domain, MaxModule);
        var integrationEvent = CreateEvent("Notes", $"{LongEvent}IntegrationEvent", EventKind.Integration, MaxModule);
        var domainFeature = CreateFeature(MaxModule, "QuarterRecalculated", domainEvent);
        var integrationFeature = CreateFeature(MaxModule, "QuarterRecalculated", integrationEvent);

        var handler = new FeatureTemplate(integrationFeature)
            .CreateFiles()
            .Single()
            .Content;

        var domainTest = new FeatureTestTemplate(domainFeature)
            .CreateFile()
            .Content;

        var integrationTest = new FeatureTestTemplate(integrationFeature)
            .CreateFile()
            .Content;

        handler.ShouldEndWith(
            $$"""
            internal sealed class QuarterRecalculatedIntegrationEventHandler
                : IIntegrationEventHandler<{{LongEvent}}IntegrationEvent>
            {
                private static readonly Error NotImplemented =
                    Error.Failure(
                        "QuarterRecalculated.NotImplemented",
                        "QuarterRecalculated in {{MaxModule}} is not implemented");

                public Task<Result<Unit>> Handle(
                    {{LongEvent}}IntegrationEvent integrationEvent,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Failure(NotImplemented));
            }

            """,
            Case.Sensitive);

        domainTest.ShouldContain(
            $$"""
                    var domainEvent = new {{LongEvent}}DomainEvent(
                        App.Time.GetUtcNow(),
                        Guid.CreateVersion7(App.Time.GetUtcNow()));

                    var dispatched = await App.DispatchAsync<I{{MaxModule}}Module>(
                        domainEvent);

                    await App.ProcessQueuesAsync();

                    dispatched.ShouldBe(Result.Success());
            """,
            Case.Sensitive);

        integrationTest.ShouldContain(
            $$"""
                    await App.PublishAsync(integrationEvent);
                    await App.ProcessQueuesAsync();
                    var inboxRows = await App.Database.ReadQueueRowsAsync(
                        {{MaxModule}}DbContext.SchemaName,
                        InboxMessage.Table);

                    inboxRows
            """,
            Case.Sensitive);
    }

    [Theory]
    [InlineData("Notes", "NoteCreated", "Domain")]
    [InlineData("Tags", "NoteCreated", "Integration")]
    [InlineData(LongModule, "QuarterRecalculated", "Domain")]
    [InlineData(LongModule, "QuarterRecalculated", "Integration")]
    [InlineData(MaxModule, "QuarterRecalculated", "Domain")]
    [InlineData(MaxModule, "QuarterRecalculated", "Integration")]
    [InlineData("Notes", "RecalculateEveryReservationAdjustmentTotalForTheWholeQuarter", "Integration")]
    [InlineData("Notes", "RecalculateEveryReservationAdjustmentTotalForTheWholeQuarter", "Domain")]
    public void Every_generated_line_of_an_event_handler_fits_in_120_characters(
        String moduleName,
        String name,
        String kind)
    {
        var eventKind = Enum.Parse<EventKind>(kind);
        var handledEvent = CreateEvent("Owners", $"{LongEvent}{kind}Event", eventKind, moduleName);
        var feature = CreateFeature(moduleName, name, handledEvent);

        var longLines = new FeatureTemplate(feature)
            .CreateFiles()
            .Append(new FeatureTestTemplate(feature).CreateFile())
            .SelectMany(newFile => newFile.Content.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }

    private static EventDeclaration CreateEvent(String ownerModule, String name, EventKind kind, String handlingModule)
    {
        var owner = kind == EventKind.Domain ? handlingModule : ownerModule;
        var eventNamespace = kind == EventKind.Domain
            ? $"Acme.Shop.Modules.{owner}.Domain.Quarters.Events"
            : $"Acme.Shop.Modules.{owner}.Contracts.IntegrationEvents";

        return new EventDeclaration(
            name,
            eventNamespace,
            kind,
            owner,
            [MemberSample.Of(Clock), MemberSample.Of($"Guid.CreateVersion7({Clock})")]);
    }

    private static ScaffoldedFeature CreateFeature(String moduleName, String name, EventDeclaration handledEvent) =>
        new(
            SampleModules.Create(moduleName),
            new FeatureRequest(moduleName, name, FeatureKind.EventHandler, EndpointMethod.None)
            {
                EventType = handledEvent.Name,
            })
        {
            Event = handledEvent,
        };
}
