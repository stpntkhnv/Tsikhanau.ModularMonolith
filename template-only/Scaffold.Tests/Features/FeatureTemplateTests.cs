using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class FeatureTemplateTests
{
    private const String NotesFeatures = "src/Modules/Notes/Acme.Shop.Modules.Notes/Features";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String LongModule = "InventoryReservationAdjustments";

    private const String LongFeature = "RecalculateReservationAdjustmentTotals";

    private const String MaxModule = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const String MaxModuleFeature = "RecalculateAdjustmentTotals";

    private const String LongNotesFeature = "RecalculateEveryReservationAdjustmentTotalForTheWholeQuarter";

    private const String ArchiveNoteCommand =
        """
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes.Features.ArchiveNote;

        internal sealed record ArchiveNoteCommand : INotesCommand<Unit>;

        """;

    private const String ArchiveNoteCommandHandler =
        """
        using Tsikhanau.Monolith.Application.Commands;
        using Tsikhanau.Railway;

        namespace Acme.Shop.Modules.Notes.Features.ArchiveNote;

        internal sealed class ArchiveNoteCommandHandler : ICommandHandler<ArchiveNoteCommand, Unit>
        {
            private static readonly Error NotImplemented =
                Error.Failure("ArchiveNote.NotImplemented", "ArchiveNote in Notes is not implemented");

            public Task<Result<Unit>> Handle(ArchiveNoteCommand command, CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure(NotImplemented));
        }

        """;

    [Fact]
    public void Command_without_an_endpoint_is_its_command_and_its_handler_that_is_not_implemented()
    {
        var feature = CreateFeature("Notes", "ArchiveNote", EndpointMethod.None);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile($"{NotesFeatures}/ArchiveNote/ArchiveNoteCommand.cs", ArchiveNoteCommand),
            new NewFile($"{NotesFeatures}/ArchiveNote/ArchiveNoteCommandHandler.cs", ArchiveNoteCommandHandler),
        ]);
    }

    [Fact]
    public void Post_command_adds_an_empty_request_and_an_endpoint_that_answers_no_content()
    {
        var feature = CreateFeature("Notes", "CreateNote", EndpointMethod.Post);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{NotesFeatures}/CreateNote/CreateNoteCommand.cs",
                ArchiveNoteCommand.Replace("ArchiveNote", "CreateNote", StringComparison.Ordinal)),
            new NewFile(
                $"{NotesFeatures}/CreateNote/CreateNoteCommandHandler.cs",
                ArchiveNoteCommandHandler.Replace("ArchiveNote", "CreateNote", StringComparison.Ordinal)),
            new NewFile(
                $"{NotesFeatures}/CreateNote/CreateNoteRequest.cs",
                """
                namespace Acme.Shop.Modules.Notes.Features.CreateNote;

                internal sealed record CreateNoteRequest;

                """),
            new NewFile(
                $"{NotesFeatures}/CreateNote/CreateNoteEndpoint.cs",
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Http.HttpResults;
                using Microsoft.AspNetCore.Routing;
                using Tsikhanau.Monolith.Api.Endpoints;

                namespace Acme.Shop.Modules.Notes.Features.CreateNote;

                internal sealed class CreateNoteEndpoint : IEndpoint
                {
                    public void Map(IEndpointRouteBuilder endpoints) =>
                        endpoints
                            .MapPost("/create-note", CreateNoteAsync)
                            .WithName("Notes.CreateNote");

                    private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> CreateNoteAsync(
                        CreateNoteRequest request,
                        INotesModule notesModule,
                        CancellationToken cancellationToken) =>
                        notesModule
                            .ExecuteCommandAsync(new CreateNoteCommand(), cancellationToken)
                            .ToNoContentAsync();
                }

                """),
        ]);
    }

    [Fact]
    public void Put_command_maps_put_with_its_request()
    {
        var feature = CreateFeature("Notes", "RenameNote", EndpointMethod.Put);

        var files = new FeatureTemplate(feature).CreateFiles();

        var paths = files.Select(newFile => newFile.Path);
        var endpoint = ReadFile(files, "RenameNoteEndpoint.cs");

        paths.ShouldBe(
        [
            $"{NotesFeatures}/RenameNote/RenameNoteCommand.cs",
            $"{NotesFeatures}/RenameNote/RenameNoteCommandHandler.cs",
            $"{NotesFeatures}/RenameNote/RenameNoteRequest.cs",
            $"{NotesFeatures}/RenameNote/RenameNoteEndpoint.cs",
        ]);

        endpoint.ShouldEndWith(
            """
            internal sealed class RenameNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapPut("/rename-note", RenameNoteAsync)
                        .WithName("Notes.RenameNote");

                private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> RenameNoteAsync(
                    RenameNoteRequest request,
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteCommandAsync(new RenameNoteCommand(), cancellationToken)
                        .ToNoContentAsync();
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Delete_command_maps_delete_without_a_request()
    {
        var feature = CreateFeature("Notes", "DeleteNote", EndpointMethod.Delete);

        var files = new FeatureTemplate(feature).CreateFiles();

        var paths = files.Select(newFile => newFile.Path);
        var endpoint = ReadFile(files, "DeleteNoteEndpoint.cs");

        paths.ShouldBe(
        [
            $"{NotesFeatures}/DeleteNote/DeleteNoteCommand.cs",
            $"{NotesFeatures}/DeleteNote/DeleteNoteCommandHandler.cs",
            $"{NotesFeatures}/DeleteNote/DeleteNoteEndpoint.cs",
        ]);

        endpoint.ShouldEndWith(
            """
            internal sealed class DeleteNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapDelete("/delete-note", DeleteNoteAsync)
                        .WithName("Notes.DeleteNote");

                private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> DeleteNoteAsync(
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteCommandAsync(new DeleteNoteCommand(), cancellationToken)
                        .ToNoContentAsync();
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Test_of_a_command_without_an_endpoint_goes_through_the_facade()
    {
        var feature = CreateFeature("Notes", "ArchiveNote", EndpointMethod.None);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                $"{NotesTests}/ArchiveNoteTests.cs",
                """
                using Acme.Shop.Modules.Notes;
                using Acme.Shop.Modules.Notes.Features.ArchiveNote;
                using Acme.Shop.Testing.App;
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Railway;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class ArchiveNoteTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Archive_note_succeeds_through_the_facade()
                    {
                        var notesModule = App.Services.GetRequiredService<INotesModule>();

                        var result = await notesModule.ExecuteCommandAsync(new ArchiveNoteCommand(), CancellationToken);

                        result.ShouldBe(Result.Success());
                    }
                }

                """));
    }

    [Fact]
    public void Test_of_a_post_command_sends_an_empty_body_and_reads_it_before_it_checks_the_status()
    {
        var feature = CreateFeature("Notes", "CreateNote", EndpointMethod.Post);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                $"{NotesTests}/CreateNoteTests.cs",
                """
                using System.Net;
                using System.Net.Http.Json;
                using Acme.Shop.Testing.App;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class CreateNoteTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Create_note_answers_no_content()
                    {
                        using var httpClient = App.CreateClient();

                        var response = await httpClient.PostAsJsonAsync("/notes/create-note", new { }, CancellationToken);
                        var body = await response.Content.ReadAsStringAsync(CancellationToken);

                        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, body);
                    }
                }

                """));
    }

    [Theory]
    [InlineData(
        "Put",
        "RenameNote",
        "var response = await httpClient.PutAsJsonAsync(\"/notes/rename-note\", new { }, CancellationToken);")]
    [InlineData(
        "Delete",
        "DeleteNote",
        "var response = await httpClient.DeleteAsync(\"/notes/delete-note\", CancellationToken);")]
    public void Test_of_a_put_or_delete_command_sends_its_method(String method, String name, String request)
    {
        var feature = CreateFeature("Notes", name, Enum.Parse<EndpointMethod>(method));

        var lines = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content
            .Split('\n')
            .Select(line => line.Trim());

        lines.ShouldContain(request);
    }

    [Fact]
    public void Test_of_a_delete_command_uses_no_json_extensions()
    {
        var feature = CreateFeature("Notes", "DeleteNote", EndpointMethod.Delete);

        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        test.ShouldStartWith(
            """
            using System.Net;
            using Acme.Shop.Testing.App;

            namespace Acme.Shop.IntegrationTests.Modules.Notes;
            """,
            Case.Sensitive);
    }

    [Theory]
    [InlineData("Notes", "CreateNote", "/create-note", "/notes/create-note")]
    [InlineData("Version2Items", "Import2Files", "/import2-files", "/version2-items/import2-files")]
    [InlineData("HTTPLogs", "PurgeHTTPLogs", "/purge-http-logs", "/http-logs/purge-http-logs")]
    public void Endpoint_maps_the_feature_in_kebab_case_and_the_test_calls_it_under_the_module_group(
        String moduleName,
        String name,
        String route,
        String path)
    {
        var feature = CreateFeature(moduleName, name, EndpointMethod.Post);

        var endpoint = ReadFile(new FeatureTemplate(feature).CreateFiles(), $"{name}Endpoint.cs");
        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        endpoint.ShouldContain($"            .MapPost(\"{route}\", {name}Async)\n", Case.Sensitive);
        test.ShouldContain(
            $"httpClient.PostAsJsonAsync(\"{path}\", new {{ }}, CancellationToken);\n",
            Case.Sensitive);
    }

    [Fact]
    public void Facade_parameter_of_a_module_with_an_acronym_is_camel_case()
    {
        var feature = CreateFeature("HTTPLogs", "PurgeLogs", EndpointMethod.Delete);

        var endpoint = ReadFile(new FeatureTemplate(feature).CreateFiles(), "PurgeLogsEndpoint.cs");

        endpoint.ShouldContain(
            """
                    IHTTPLogsModule httpLogsModule,
                    CancellationToken cancellationToken) =>
                    httpLogsModule
                        .ExecuteCommandAsync(new PurgeLogsCommand(), cancellationToken)
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Long_names_wrap_the_handler_the_endpoint_and_the_test()
    {
        var feature = CreateFeature(LongModule, LongFeature, EndpointMethod.Post);
        var files = new FeatureTemplate(feature).CreateFiles();

        var handler = ReadFile(files, $"{LongFeature}CommandHandler.cs");
        var endpoint = ReadFile(files, $"{LongFeature}Endpoint.cs");
        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        handler.ShouldEndWith(
            """
            internal sealed class RecalculateReservationAdjustmentTotalsCommandHandler
                : ICommandHandler<RecalculateReservationAdjustmentTotalsCommand, Unit>
            {
                private static readonly Error NotImplemented =
                    Error.Failure(
                        "RecalculateReservationAdjustmentTotals.NotImplemented",
                        "RecalculateReservationAdjustmentTotals in InventoryReservationAdjustments is not implemented");

                public Task<Result<Unit>> Handle(
                    RecalculateReservationAdjustmentTotalsCommand command,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Failure(NotImplemented));
            }

            """,
            Case.Sensitive);

        endpoint.ShouldEndWith(
            """
                private static Task<Results<
                    NoContent,
                    ValidationProblem,
                    ProblemHttpResult>> RecalculateReservationAdjustmentTotalsAsync(
                    RecalculateReservationAdjustmentTotalsRequest request,
                    IInventoryReservationAdjustmentsModule inventoryReservationAdjustmentsModule,
                    CancellationToken cancellationToken) =>
                    inventoryReservationAdjustmentsModule
                        .ExecuteCommandAsync(new RecalculateReservationAdjustmentTotalsCommand(), cancellationToken)
                        .ToNoContentAsync();
            }

            """,
            Case.Sensitive);

        test.ShouldContain(
            """
                    var response = await httpClient.PostAsJsonAsync(
                        "/inventory-reservation-adjustments/recalculate-reservation-adjustment-totals",
                        new { },
                        CancellationToken);

                    var body = await response.Content.ReadAsStringAsync(CancellationToken);

            """,
            Case.Sensitive);
    }

    [Theory]
    [InlineData("Notes", "ArchiveNote", "None")]
    [InlineData("Notes", "CreateNote", "Post")]
    [InlineData(LongModule, LongFeature, "None")]
    [InlineData(LongModule, LongFeature, "Post")]
    [InlineData(LongModule, LongFeature, "Put")]
    [InlineData(LongModule, LongFeature, "Delete")]
    [InlineData(MaxModule, MaxModuleFeature, "None")]
    [InlineData("Notes", LongNotesFeature, "None")]
    [InlineData("Notes", LongNotesFeature, "Post")]
    public void Every_generated_line_fits_in_120_characters(String moduleName, String name, String method)
    {
        var feature = CreateFeature(moduleName, name, Enum.Parse<EndpointMethod>(method));

        var longLines = new FeatureTemplate(feature)
            .CreateFiles()
            .Append(new FeatureTestTemplate(feature).CreateFile())
            .SelectMany(newFile => newFile.Content.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }

    private static ScaffoldedFeature CreateFeature(String moduleName, String name, EndpointMethod method) =>
        new(SampleModules.Create(moduleName), new FeatureRequest(moduleName, name, FeatureKind.Command, method));

    private static String ReadFile(IEnumerable<NewFile> files, String fileName) =>
        files
            .Single(newFile => Path.GetFileName(newFile.Path) == fileName)
            .Content;
}
