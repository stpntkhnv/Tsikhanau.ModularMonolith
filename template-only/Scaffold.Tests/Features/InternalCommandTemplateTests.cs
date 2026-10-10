using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class InternalCommandTemplateTests
{
    private const String TagsFeatures = "src/Modules/Tags/Acme.Shop.Modules.Tags/Features";

    private const String LongModule = "InventoryReservationAdjustments";

    private const String MaxModule = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const String LongFeature = "RecalculateEveryReservationAdjustmentTotalOfTheMonth";

    private const String LongKebabName = "recalculate-every-reservation-adjustment-total-of-the-month";

    [Fact]
    public void Internal_command_is_a_named_record_and_a_handler_that_is_not_implemented()
    {
        var feature = CreateFeature("Tags", "RecountTags");

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{TagsFeatures}/RecountTags/RecountTagsInternalCommand.cs",
                """
                using Tsikhanau.Monolith.Application.Commands;

                namespace Acme.Shop.Modules.Tags.Features.RecountTags;

                [InternalCommandName("recount-tags")]
                internal sealed record RecountTagsInternalCommand(Guid Id) : InternalCommand(Id);

                """),
            new NewFile(
                $"{TagsFeatures}/RecountTags/RecountTagsInternalCommandHandler.cs",
                """
                using Tsikhanau.Monolith.Application.Commands;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.Tags.Features.RecountTags;

                internal sealed class RecountTagsInternalCommandHandler : ICommandHandler<RecountTagsInternalCommand, Unit>
                {
                    private static readonly Error NotImplemented =
                        Error.Failure("RecountTags.NotImplemented", "RecountTags in Tags is not implemented");

                    public Task<Result<Unit>> Handle(RecountTagsInternalCommand command, CancellationToken cancellationToken) =>
                        Task.FromResult(Result.Failure(NotImplemented));
                }

                """),
        ]);
    }

    [Fact]
    public void Test_enqueues_the_internal_command_runs_the_queues_and_checks_that_its_row_is_processed()
    {
        var feature = CreateFeature("Tags", "RecountTags");

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                "src/Tests/IntegrationTests/Modules/Tags/RecountTagsTests.cs",
                """
                using Acme.Shop.Modules.Tags;
                using Acme.Shop.Modules.Tags.Features.RecountTags;
                using Acme.Shop.Modules.Tags.Infrastructure.Persistence;
                using Acme.Shop.Testing.App;
                using Tsikhanau.Monolith.Infrastructure.InternalCommands;

                namespace Acme.Shop.IntegrationTests.Modules.Tags;

                public sealed class RecountTagsTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    [Fact]
                    public async Task Recount_tags_runs_when_the_queues_run()
                    {
                        var internalCommand = new RecountTagsInternalCommand(Guid.CreateVersion7(App.Time.GetUtcNow()));

                        await App.EnqueueAsync<ITagsModule>(internalCommand);
                        await App.ProcessQueuesAsync();
                        var internalCommandRows = await App.Database.ReadQueueRowsAsync(
                            TagsDbContext.SchemaName,
                            InternalCommandMessage.Table);

                        internalCommandRows
                            .Select(internalCommandRow => (internalCommandRow.Id, internalCommandRow.IsProcessed))
                            .ShouldBe([(internalCommand.Id, true)]);
                    }
                }

                """));
    }

    [Theory]
    [InlineData("RecountTags", "recount-tags")]
    [InlineData("PurgeHTTPLogs", "purge-http-logs")]
    [InlineData("Import2Files", "import2-files")]
    [InlineData("Version2Items", "version2-items")]
    [InlineData("Sweep", "sweep")]
    public void Internal_command_name_is_the_feature_name_in_kebab_case(String name, String internalCommandName)
    {
        var feature = CreateFeature("Tags", name);

        var internalCommand = new FeatureTemplate(feature)
            .CreateFiles()[0]
            .Content;

        internalCommand.ShouldContain(
            $"""

             [InternalCommandName("{internalCommandName}")]
             internal sealed record {name}InternalCommand(Guid Id) : InternalCommand(Id);

             """,
            Case.Sensitive);
    }

    [Fact]
    public void Long_names_wrap_the_record_the_handler_and_the_construction_in_the_test()
    {
        var feature = CreateFeature("Notes", LongFeature);

        var files = new FeatureTemplate(feature).CreateFiles();

        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        files[0].Content.ShouldEndWith(
            $$"""
            [InternalCommandName("{{LongKebabName}}")]
            internal sealed record {{LongFeature}}InternalCommand(Guid Id)
                : InternalCommand(Id);

            """,
            Case.Sensitive);

        files[1].Content.ShouldEndWith(
            $$"""
            internal sealed class {{LongFeature}}InternalCommandHandler
                : ICommandHandler<{{LongFeature}}InternalCommand, Unit>
            {
                private static readonly Error NotImplemented =
                    Error.Failure(
                        "{{LongFeature}}.NotImplemented",
                        "{{LongFeature}} in Notes is not implemented");

                public Task<Result<Unit>> Handle(
                    {{LongFeature}}InternalCommand command,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Failure(NotImplemented));
            }

            """,
            Case.Sensitive);

        test.ShouldContain(
            $$"""
                    var internalCommand = new {{LongFeature}}InternalCommand(
                        Guid.CreateVersion7(App.Time.GetUtcNow()));

                    await App.EnqueueAsync<INotesModule>(internalCommand);
            """,
            Case.Sensitive);
    }

    [Theory]
    [InlineData("Tags", "RecountTags")]
    [InlineData("Notes", LongFeature)]
    [InlineData(LongModule, "RecalculateEveryReservationAdjustmentTotal")]
    [InlineData(MaxModule, "RecountTags")]
    public void Every_generated_line_of_an_internal_command_fits_in_120_characters(String moduleName, String name)
    {
        var feature = CreateFeature(moduleName, name);

        var longLines = new FeatureTemplate(feature)
            .CreateFiles()
            .Append(new FeatureTestTemplate(feature).CreateFile())
            .SelectMany(newFile => newFile.Content.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }

    private static ScaffoldedFeature CreateFeature(String moduleName, String name) =>
        new(
            SampleModules.Create(moduleName),
            new FeatureRequest(moduleName, name, FeatureKind.InternalCommand, EndpointMethod.None));
}
