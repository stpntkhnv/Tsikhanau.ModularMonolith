using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Monolith.Scaffold.Tests.Support;
using Tsikhanau.Monolith.Scaffold.Transactions;

namespace Tsikhanau.Monolith.Scaffold.Tests.Features;

public sealed class FeatureTemplateTests
{
    private const String NotesFeatures = "src/Modules/Notes/Acme.Shop.Modules.Notes/Features";

    private const String NotesTests = "src/Tests/IntegrationTests/Modules/Notes";

    private const String TagsFeatures = "src/Modules/Tags/Acme.Shop.Modules.Tags/Features";

    private const String TagsQueries = "src/Modules/Tags/Acme.Shop.Modules.Tags.Contracts/Queries";

    private const String LongModule = "InventoryReservationAdjustments";

    private const String LongFeature = "RecalculateReservationAdjustmentTotals";

    private const String MaxModule = "Abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const String MaxModuleFeature = "RecalculateAdjustmentTotals";

    private const String LongNotesFeature = "RecalculateEveryReservationAdjustmentTotalForTheWholeQuarter";

    private const String LongerNotesFeature = $"{LongNotesFeature}AndTheYear";

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
    public void Collection_route_maps_the_module_group_itself_and_the_test_calls_the_group()
    {
        var feature = CreateFeature("Notes", "AddNote", FeatureKind.Command, EndpointMethod.Post, String.Empty);

        var endpoint = ReadFile(new FeatureTemplate(feature).CreateFiles(), "AddNoteEndpoint.cs");
        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        endpoint.ShouldEndWith(
            """
            internal sealed class AddNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapPost(String.Empty, AddNoteAsync)
                        .WithName("Notes.AddNote");

                private static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> AddNoteAsync(
                    AddNoteRequest request,
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteCommandAsync(new AddNoteCommand(), cancellationToken)
                        .ToNoContentAsync();
            }

            """,
            Case.Sensitive);

        test.ShouldEndWith(
            """
                [Fact]
                public async Task Add_note_answers_no_content()
                {
                    using var httpClient = App.CreateClient();

                    var response = await httpClient.PostAsJsonAsync("/notes", new { }, CancellationToken);
                    var body = await response.Content.ReadAsStringAsync(CancellationToken);

                    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, body);
                }
            }

            """,
            Case.Sensitive);
    }

    [Fact]
    public void Item_route_maps_its_guid_id_and_the_test_calls_it_with_a_sample_guid()
    {
        var feature = CreateFeature("Notes", "GetNote", FeatureKind.Query, EndpointMethod.Get, "/{noteId:guid}");

        var endpoint = ReadFile(new FeatureTemplate(feature).CreateFiles(), "GetNoteEndpoint.cs");
        var test = new FeatureTestTemplate(feature).CreateFile();

        endpoint.ShouldEndWith(
            """
            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapGet("/{noteId:guid}", GetNoteAsync)
                        .WithName("Notes.GetNote");

                private static Task<Results<Ok<NoteDto>, ValidationProblem, ProblemHttpResult>> GetNoteAsync(
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteQueryAsync(new GetNoteQuery(), cancellationToken)
                        .ToOkAsync();
            }

            """,
            Case.Sensitive);

        test.ShouldBe(
            new NewFile(
                $"{NotesTests}/GetNoteTests.cs",
                """
                using System.Net;
                using Acme.Shop.Testing.App;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class GetNoteTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Get_note_answers_ok()
                    {
                        using var httpClient = App.CreateClient();

                        var response = await httpClient.GetAsync("/notes/017f22e2-79b0-7cc3-98c4-dc0c0c07398f", CancellationToken);
                        var body = await response.Content.ReadAsStringAsync(CancellationToken);

                        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
                    }
                }

                """));
    }

    [Fact]
    public void Test_of_a_route_with_two_ids_fills_each_with_the_sample_guid()
    {
        var feature = CreateFeature(
            "Notes",
            "MoveTag",
            FeatureKind.Command,
            EndpointMethod.Put,
            "/{noteId:guid}/tags/{tagId:guid}");

        var endpoint = ReadFile(new FeatureTemplate(feature).CreateFiles(), "MoveTagEndpoint.cs");
        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        endpoint.ShouldContain(
            "            .MapPut(\"/{noteId:guid}/tags/{tagId:guid}\", MoveTagAsync)\n",
            Case.Sensitive);

        test.ShouldContain(
            """
                    var response = await httpClient.PutAsJsonAsync(
                        "/notes/017f22e2-79b0-7cc3-98c4-dc0c0c07398f/tags/017f22e2-79b0-7cc3-98c4-dc0c0c07398f",
                        new { },
                        CancellationToken);

            """,
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

    [Fact]
    public void Query_without_an_endpoint_is_its_query_its_result_record_and_its_handler_that_is_not_implemented()
    {
        var feature = CreateFeature("Notes", "CountNotes", FeatureKind.Query, EndpointMethod.None);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{NotesFeatures}/CountNotes/CountNotesQuery.cs",
                """
                using Acme.Shop.Modules.Notes.Contracts;

                namespace Acme.Shop.Modules.Notes.Features.CountNotes;

                internal sealed record CountNotesQuery : INotesQuery<CountNotesDto>;

                """),
            new NewFile(
                $"{NotesFeatures}/CountNotes/CountNotesDto.cs",
                """
                namespace Acme.Shop.Modules.Notes.Features.CountNotes;

                internal sealed record CountNotesDto;

                """),
            new NewFile(
                $"{NotesFeatures}/CountNotes/CountNotesQueryHandler.cs",
                """
                using Tsikhanau.Monolith.Application.Queries;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.Notes.Features.CountNotes;

                internal sealed class CountNotesQueryHandler : IQueryHandler<CountNotesQuery, CountNotesDto>
                {
                    private static readonly Error NotImplemented =
                        Error.Failure("CountNotes.NotImplemented", "CountNotes in Notes is not implemented");

                    public Task<Result<CountNotesDto>> Handle(CountNotesQuery query, CancellationToken cancellationToken) =>
                        Task.FromResult(Result.Failure<CountNotesDto>(NotImplemented));
                }

                """),
        ]);
    }

    [Fact]
    public void Get_query_answers_its_result_record_through_an_endpoint_without_the_leading_get()
    {
        var feature = CreateFeature("Notes", "GetNoteSummary", FeatureKind.Query, EndpointMethod.Get);

        var files = new FeatureTemplate(feature).CreateFiles();

        var paths = files.Select(newFile => newFile.Path);
        var endpoint = ReadFile(files, "GetNoteSummaryEndpoint.cs");

        paths.ShouldBe(
        [
            $"{NotesFeatures}/GetNoteSummary/GetNoteSummaryQuery.cs",
            $"{NotesFeatures}/GetNoteSummary/NoteSummaryDto.cs",
            $"{NotesFeatures}/GetNoteSummary/GetNoteSummaryQueryHandler.cs",
            $"{NotesFeatures}/GetNoteSummary/GetNoteSummaryEndpoint.cs",
        ]);

        endpoint.ShouldBe(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace Acme.Shop.Modules.Notes.Features.GetNoteSummary;

            internal sealed class GetNoteSummaryEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapGet("/note-summary", GetNoteSummaryAsync)
                        .WithName("Notes.GetNoteSummary");

                private static Task<Results<Ok<NoteSummaryDto>, ValidationProblem, ProblemHttpResult>> GetNoteSummaryAsync(
                    INotesModule notesModule,
                    CancellationToken cancellationToken) =>
                    notesModule
                        .ExecuteQueryAsync(new GetNoteSummaryQuery(), cancellationToken)
                        .ToOkAsync();
            }

            """);
    }

    [Fact]
    public void Public_query_puts_the_query_and_its_result_record_into_the_contracts_and_its_handler_into_the_module()
    {
        var feature = CreateFeature("Tags", "GetTag", FeatureKind.PublicQuery, EndpointMethod.None);

        var files = new FeatureTemplate(feature).CreateFiles();

        files.ShouldBe(
        [
            new NewFile(
                $"{TagsQueries}/GetTag/GetTagQuery.cs",
                """
                namespace Acme.Shop.Modules.Tags.Contracts.Queries.GetTag;

                public sealed record GetTagQuery : ITagsQuery<TagDto>;

                """),
            new NewFile(
                $"{TagsQueries}/GetTag/TagDto.cs",
                """
                namespace Acme.Shop.Modules.Tags.Contracts.Queries.GetTag;

                public sealed record TagDto;

                """),
            new NewFile(
                $"{TagsFeatures}/GetTag/GetTagQueryHandler.cs",
                """
                using Acme.Shop.Modules.Tags.Contracts.Queries.GetTag;
                using Tsikhanau.Monolith.Application.Queries;
                using Tsikhanau.Railway;

                namespace Acme.Shop.Modules.Tags.Features.GetTag;

                internal sealed class GetTagQueryHandler : IQueryHandler<GetTagQuery, TagDto>
                {
                    private static readonly Error NotImplemented =
                        Error.Failure("GetTag.NotImplemented", "GetTag in Tags is not implemented");

                    public Task<Result<TagDto>> Handle(GetTagQuery query, CancellationToken cancellationToken) =>
                        Task.FromResult(Result.Failure<TagDto>(NotImplemented));
                }

                """),
        ]);
    }

    [Theory]
    [InlineData(
        "Query",
        "Get",
        "GetNoteSummary",
        "NoteSummaryDto.cs",
        ".MapGet(\"/note-summary\", GetNoteSummaryAsync)")]
    [InlineData("Query", "Get", "CountNotes", "CountNotesDto.cs", ".MapGet(\"/count-notes\", CountNotesAsync)")]
    [InlineData("Query", "Get", "Getaway", "GetawayDto.cs", ".MapGet(\"/getaway\", GetawayAsync)")]
    [InlineData("Query", "Get", "Get", "GetDto.cs", ".MapGet(\"/get\", GetAsync)")]
    [InlineData("Query", "Get", "GetHTTPLogs", "HTTPLogsDto.cs", ".MapGet(\"/http-logs\", GetHTTPLogsAsync)")]
    [InlineData("Command", "Post", "GetBackup", "GetBackupRequest.cs", ".MapPost(\"/get-backup\", GetBackupAsync)")]
    public void Query_drops_a_leading_get_from_its_result_record_and_its_route_and_a_command_keeps_it(
        String kind,
        String method,
        String name,
        String recordFile,
        String mapCall)
    {
        var feature = CreateFeature("Notes", name, Enum.Parse<FeatureKind>(kind), Enum.Parse<EndpointMethod>(method));

        var files = new FeatureTemplate(feature).CreateFiles();

        var fileNames = files.Select(newFile => Path.GetFileName(newFile.Path));
        var endpointLines = ReadFile(files, $"{name}Endpoint.cs")
            .Split('\n')
            .Select(line => line.Trim());

        fileNames.ShouldContain(recordFile);
        endpointLines.ShouldContain(mapCall);
    }

    [Fact]
    public void Test_of_a_query_without_an_endpoint_compares_the_result_with_an_empty_result_record()
    {
        var feature = CreateFeature("Notes", "CountNotes", FeatureKind.Query, EndpointMethod.None);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                $"{NotesTests}/CountNotesTests.cs",
                """
                using Acme.Shop.Modules.Notes;
                using Acme.Shop.Modules.Notes.Features.CountNotes;
                using Acme.Shop.Testing.App;
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Railway;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class CountNotesTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Count_notes_succeeds_through_the_facade()
                    {
                        var notesModule = App.Services.GetRequiredService<INotesModule>();

                        var result = await notesModule.ExecuteQueryAsync(new CountNotesQuery(), CancellationToken);

                        result.ShouldBe(Result.Success(new CountNotesDto()));
                    }
                }

                """));
    }

    [Fact]
    public void Test_of_a_get_query_calls_the_route_and_checks_ok_with_the_body_as_the_message()
    {
        var feature = CreateFeature("Notes", "GetNoteSummary", FeatureKind.Query, EndpointMethod.Get);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                $"{NotesTests}/GetNoteSummaryTests.cs",
                """
                using System.Net;
                using Acme.Shop.Testing.App;

                namespace Acme.Shop.IntegrationTests.Modules.Notes;

                public sealed class GetNoteSummaryTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Get_note_summary_answers_ok()
                    {
                        using var httpClient = App.CreateClient();

                        var response = await httpClient.GetAsync("/notes/note-summary", CancellationToken);
                        var body = await response.Content.ReadAsStringAsync(CancellationToken);

                        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
                    }
                }

                """));
    }

    [Fact]
    public void Test_of_a_public_query_goes_through_the_query_facade_a_neighbour_sees()
    {
        var feature = CreateFeature("Tags", "GetTag", FeatureKind.PublicQuery, EndpointMethod.None);

        var test = new FeatureTestTemplate(feature).CreateFile();

        test.ShouldBe(
            new NewFile(
                "src/Tests/IntegrationTests/Modules/Tags/GetTagTests.cs",
                """
                using Acme.Shop.Modules.Tags.Contracts;
                using Acme.Shop.Modules.Tags.Contracts.Queries.GetTag;
                using Acme.Shop.Testing.App;
                using Microsoft.Extensions.DependencyInjection;
                using Tsikhanau.Railway;

                namespace Acme.Shop.IntegrationTests.Modules.Tags;

                public sealed class GetTagTests(AppFixture appFixture) : AppTest(appFixture)
                {
                    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

                    [Fact]
                    public async Task Get_tag_succeeds_through_the_query_facade()
                    {
                        var tagsQueryFacade = App.Services.GetRequiredService<ITagsQueryFacade>();

                        var result = await tagsQueryFacade.ExecuteQueryAsync(new GetTagQuery(), CancellationToken);

                        result.ShouldBe(Result.Success(new TagDto()));
                    }
                }

                """));
    }

    [Fact]
    public void Query_facade_variable_of_a_module_with_an_acronym_is_camel_case()
    {
        var feature = CreateFeature("HTTPLogs", "GetLog", FeatureKind.PublicQuery, EndpointMethod.None);

        var test = new FeatureTestTemplate(feature)
            .CreateFile()
            .Content;

        test.ShouldContain(
            """
                    var httpLogsQueryFacade = App.Services.GetRequiredService<IHTTPLogsQueryFacade>();

                    var result = await httpLogsQueryFacade.ExecuteQueryAsync(new GetLogQuery(), CancellationToken);
            """,
            Case.Sensitive);
    }

    [Fact]
    public void Long_names_wrap_the_query_handler_the_get_endpoint_and_the_query_test()
    {
        var getFeature = CreateFeature(LongModule, $"Get{LongFeature}", FeatureKind.Query, EndpointMethod.Get);
        var facadeFeature = CreateFeature("Notes", LongNotesFeature, FeatureKind.Query, EndpointMethod.None);
        var getFiles = new FeatureTemplate(getFeature).CreateFiles();

        var handler = ReadFile(getFiles, $"Get{LongFeature}QueryHandler.cs");
        var endpoint = ReadFile(getFiles, $"Get{LongFeature}Endpoint.cs");
        var facadeHandler = ReadFile(
            new FeatureTemplate(facadeFeature).CreateFiles(),
            $"{LongNotesFeature}QueryHandler.cs");

        var facadeTest = new FeatureTestTemplate(facadeFeature)
            .CreateFile()
            .Content;

        handler.ShouldEndWith(
            """
            internal sealed class GetRecalculateReservationAdjustmentTotalsQueryHandler
                : IQueryHandler<GetRecalculateReservationAdjustmentTotalsQuery, RecalculateReservationAdjustmentTotalsDto>
            {
                private static readonly Error NotImplemented =
                    Error.Failure(
                        "GetRecalculateReservationAdjustmentTotals.NotImplemented",
                        "GetRecalculateReservationAdjustmentTotals in InventoryReservationAdjustments is not implemented");

                public Task<Result<RecalculateReservationAdjustmentTotalsDto>> Handle(
                    GetRecalculateReservationAdjustmentTotalsQuery query,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Failure<RecalculateReservationAdjustmentTotalsDto>(NotImplemented));
            }

            """,
            Case.Sensitive);

        endpoint.ShouldEndWith(
            """
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapGet("/recalculate-reservation-adjustment-totals", GetRecalculateReservationAdjustmentTotalsAsync)
                        .WithName("InventoryReservationAdjustments.GetRecalculateReservationAdjustmentTotals");

                private static Task<Results<
                    Ok<RecalculateReservationAdjustmentTotalsDto>,
                    ValidationProblem,
                    ProblemHttpResult>> GetRecalculateReservationAdjustmentTotalsAsync(
                    IInventoryReservationAdjustmentsModule inventoryReservationAdjustmentsModule,
                    CancellationToken cancellationToken) =>
                    inventoryReservationAdjustmentsModule
                        .ExecuteQueryAsync(new GetRecalculateReservationAdjustmentTotalsQuery(), cancellationToken)
                        .ToOkAsync();
            }

            """,
            Case.Sensitive);

        facadeHandler.ShouldEndWith(
            """
                    Task.FromResult(
                        Result.Failure<RecalculateEveryReservationAdjustmentTotalForTheWholeQuarterDto>(NotImplemented));
            }

            """,
            Case.Sensitive);

        facadeTest.ShouldContain(
            """
                    var result = await notesModule.ExecuteQueryAsync(
                        new RecalculateEveryReservationAdjustmentTotalForTheWholeQuarterQuery(),
                        CancellationToken);

                    result.ShouldBe(Result.Success(new RecalculateEveryReservationAdjustmentTotalForTheWholeQuarterDto()));
            """,
            Case.Sensitive);
    }

    [Theory]
    [InlineData("Notes", "CountNotes", "Query", "None")]
    [InlineData("Notes", "GetNoteSummary", "Query", "Get")]
    [InlineData("Tags", "GetTag", "PublicQuery", "None")]
    [InlineData(LongModule, LongFeature, "Query", "None")]
    [InlineData(LongModule, LongFeature, "Query", "Get")]
    [InlineData(LongModule, LongFeature, "PublicQuery", "None")]
    [InlineData(MaxModule, MaxModuleFeature, "Query", "None")]
    [InlineData("Notes", LongNotesFeature, "Query", "None")]
    [InlineData("Notes", LongNotesFeature, "Query", "Get")]
    [InlineData("Notes", LongerNotesFeature, "Query", "Get")]
    public void Every_generated_line_of_a_query_fits_in_120_characters(
        String moduleName,
        String name,
        String kind,
        String method)
    {
        var feature = CreateFeature(
            moduleName,
            name,
            Enum.Parse<FeatureKind>(kind),
            Enum.Parse<EndpointMethod>(method));

        var longLines = new FeatureTemplate(feature)
            .CreateFiles()
            .Append(new FeatureTestTemplate(feature).CreateFile())
            .SelectMany(newFile => newFile.Content.Split('\n'))
            .Where(line => line.Length > 120);

        longLines.ShouldBeEmpty();
    }

    private static ScaffoldedFeature CreateFeature(String moduleName, String name, EndpointMethod method) =>
        CreateFeature(moduleName, name, FeatureKind.Command, method);

    private static ScaffoldedFeature CreateFeature(
        String moduleName,
        String name,
        FeatureKind kind,
        EndpointMethod method) =>
        new(SampleModules.Create(moduleName), new FeatureRequest(moduleName, name, kind, method));

    private static ScaffoldedFeature CreateFeature(
        String moduleName,
        String name,
        FeatureKind kind,
        EndpointMethod method,
        String route) =>
        new(
            SampleModules.Create(moduleName),
            new FeatureRequest(moduleName, name, kind, method) { Route = EndpointRoute.Parse(route).Value });

    private static String ReadFile(IEnumerable<NewFile> files, String fileName) =>
        files
            .Single(newFile => Path.GetFileName(newFile.Path) == fileName)
            .Content;
}
