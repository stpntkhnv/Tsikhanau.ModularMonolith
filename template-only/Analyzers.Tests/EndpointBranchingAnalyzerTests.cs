namespace Tsikhanau.Monolith.Analyzers.Tests;

public sealed class EndpointBranchingAnalyzerTests
{
    private const Int32 BranchLine = 17;

    private const String NoRepositoryRoot = "no folder above the test output holds global.json";

    private const String GoodFixtureFolder = "template-only/ArchRules.Tests/Fixtures/Sources/Acme.Modules.Good";

    private const String ConditionalInEndpointMessage =
        "An endpoint holds no branching: it maps the request to a command or query and the result to HTTP; move the "
        + "decision into the handler or the domain. Rule in AGENTS.md: Domain depends only on itself and the domain "
        + "building blocks; Features never depend on Infrastructure; endpoints go only through the facade; an "
        + "endpoint holds no branching. Playbook: docs/playbooks/add-feature.md";

    private const String GoodFixtureTypes =
        """
        global using System;
        global using System.Threading;
        global using System.Threading.Tasks;
        using Tsikhanau.Railway;

        namespace Acme.Modules.Good.Features.CreateWidget;

        internal interface IGoodModule
        {
            Task<Result<Guid>> ExecuteCommandAsync(CreateWidgetCommand command, CancellationToken cancellationToken);
        }

        internal sealed record CreateWidgetCommand(Optional<Guid> WidgetId, String Label, Decimal Price);

        internal sealed record CreateWidgetRequest(String Label, Decimal Price, Optional<Guid> WidgetId);
        """;

    private static readonly String GoodFixtureEndpointPath = Path.Combine(
        FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory)),
        GoodFixtureFolder,
        "Features/CreateWidget/CreateWidgetEndpoint.cs");

    public static TheoryData<String> Branches =>
    [
        "if (id > 0) { title = \"positive\"; }",
        "switch (id) { case 0: title = \"zero\"; break; }",
        "title = id switch { 0 => \"zero\", _ => title };",
        "title = id > 0 ? \"positive\" : title;",
        "title = title ?? \"untitled\";",
        "title ??= \"untitled\";",
        "_ = id > 0 && id < 10;",
        "_ = id < 0 || id > 10;",
        "for (var index = 0; index < id; index++) { title += index; }",
        "foreach (var item in ids) { title += item; }",
        "foreach (var (key, count) in counts) { title += key + count; }",
        "while (id > 0) { id--; }",
        "do { id--; } while (id > 0);",
        "try { id++; } finally { id--; }",
        "throw new InvalidOperationException(title);",
        "Func<String> read = () => throw new InvalidOperationException(title);",
        "goto done; done: id++;",
    ];

    [Fact]
    public async Task Diagnostic_states_the_fix_the_rule_line_and_the_playbook()
    {
        var messages = await AnalyzerRunner.GetReportedMessagesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);

                private static Ok<Int32> Get(Int32 id) => TypedResults.Ok(id > 0 ? id : 0);
            }
            """);

        messages.ShouldBe([ConditionalInEndpointMessage]);
    }

    [Theory]
    [MemberData(nameof(Branches))]
    public async Task Branching_in_an_endpoint_is_reported_where_it_starts(String branch)
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            $$"""
            using System;
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);

                private static Ok<String> Get(Int32 id, String title, Int32[] ids, Dictionary<String, Int32> counts)
                {
                    {{branch}}
                    return TypedResults.Ok(title);
                }
            }
            """);

        lines.ShouldBe([BranchLine]);
    }

    [Fact]
    public async Task Goto_case_and_goto_default_are_reported_beside_their_switch()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);

                private static Ok<Int32> Get(Int32 id)
                {
                    switch (id)
                    {
                        case 0:
                            goto case 1;
                        case 1:
                            goto default;
                        default:
                            break;
                    }

                    return TypedResults.Ok(id);
                }
            }
            """);

        lines.ShouldBe([16, 19, 21]);
    }

    [Fact]
    public async Task Branching_inside_a_lambda_passed_to_map_post_is_reported()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.CreateNote;

            internal sealed class CreateNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints.MapPost(
                        "/",
                        (String title) =>
                        {
                            if (title.Length == 0)
                            {
                                title = "untitled";
                            }

                            return TypedResults.Ok(title);
                        });
            }
            """);

        lines.ShouldBe([16]);
    }

    [Fact]
    public async Task Branching_inside_a_local_function_is_reported()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);

                private static Ok<Int32> Get(Int32 id)
                {
                    return TypedResults.Ok(Clamp(id));

                    static Int32 Clamp(Int32 value) => value > 0 ? value : 0;
                }
            }
            """);

        lines.ShouldBe([18]);
    }

    [Fact]
    public async Task Branching_in_the_second_part_of_a_partial_endpoint_in_another_file_is_reported()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourcesAsync(
            new EndpointBranchingAnalyzer(),
            [
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Routing;
                using Tsikhanau.Monolith.Api.Endpoints;

                namespace App.Modules.Notes.Features.GetNote;

                internal sealed partial class GetNoteEndpoint : IEndpoint
                {
                    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);
                }
                """,
                """
                using System;
                using Microsoft.AspNetCore.Http;
                using Microsoft.AspNetCore.Http.HttpResults;

                namespace App.Modules.Notes.Features.GetNote;

                internal sealed partial class GetNoteEndpoint
                {
                    private static Ok<Int32> Get(Int32 id)
                    {
                        while (id > 100)
                        {
                            id /= 2;
                        }

                        return TypedResults.Ok(id);
                    }
                }
                """,
            ]);

        lines.ShouldBe([11]);
    }

    [Fact]
    public async Task Endpoint_that_maps_the_request_to_a_command_and_the_result_to_http_passes()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;
            using Tsikhanau.Railway;

            namespace App.Modules.Pings.Features.CreatePing;

            internal interface IPingsModule
            {
                Task<Result<Guid>> ExecuteCommandAsync(CreatePingCommand command, CancellationToken cancellationToken);
            }

            internal sealed record CreatePingRequest(String Label, Optional<String> Nickname);

            internal sealed record CreatePingCommand(Guid PingId, String Label, Optional<String> Nickname);

            internal sealed class CreatePingEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints
                        .MapPost(String.Empty, CreateAsync)
                        .WithName("Pings.CreatePing");

                private static Task<Results<CreatedAtRoute<Guid>, ValidationProblem, ProblemHttpResult>> CreateAsync(
                    CreatePingRequest request,
                    IPingsModule pingsModule,
                    TimeProvider timeProvider,
                    CancellationToken cancellationToken) =>
                    pingsModule
                        .ExecuteCommandAsync(CreateCommand(request, timeProvider), cancellationToken)
                        .ToCreatedAsync("Pings.GetPing", pingId => new { pingId });

                private static CreatePingCommand CreateCommand(CreatePingRequest request, TimeProvider timeProvider)
                {
                    var createdOn = timeProvider.GetUtcNow();
                    return new(Guid.CreateVersion7(createdOn), request.Label, request.Nickname);
                }
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unconditional_file_result_passes()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.DownloadNote;

            internal sealed class DownloadNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}/file", Download);

                private static FileContentHttpResult Download(Int32 id) =>
                    TypedResults.File(BitConverter.GetBytes(id), "application/octet-stream", "note.bin");
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Typed_results_imported_with_using_static_pass()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;
            using static Microsoft.AspNetCore.Http.TypedResults;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id}", Get);

                private static Ok<Int32> Get(Int32 id) => Ok(id);
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Union_of_results_in_the_signature_passes()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Http.HttpResults;
            using Microsoft.AspNetCore.Routing;
            using Tsikhanau.Monolith.Api.Endpoints;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class GetNoteEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{title}", GetAsync);

                private static Task<Results<Ok<String>, NotFound>> GetAsync(String title) =>
                    Task.FromResult<Results<Ok<String>, NotFound>>(TypedResults.Ok(title));
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Branching_in_a_class_that_is_no_endpoint_passes()
    {
        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourceAsync(
            new EndpointBranchingAnalyzer(),
            """
            using System;

            namespace App.Modules.Notes.Features.GetNote;

            internal sealed class NoteTitleFormatter
            {
                public String Format(String title)
                {
                    if (title.Length == 0)
                    {
                        return "untitled";
                    }

                    return title;
                }
            }
            """);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Good_fixture_endpoint_of_the_arch_rule_tests_holds_no_branching()
    {
        var fixtureEndpoint = await File.ReadAllTextAsync(
            GoodFixtureEndpointPath,
            TestContext.Current.CancellationToken);

        var lines = await AnalyzerRunner.GetReportedLinesOfValidSourcesAsync(
            new EndpointBranchingAnalyzer(),
            [fixtureEndpoint, GoodFixtureTypes]);

        lines.ShouldBeEmpty();
    }

    private static String FindRepositoryRoot(DirectoryInfo folder) =>
        File.Exists(Path.Combine(folder.FullName, "global.json"))
            ? folder.FullName
            : FindRepositoryRoot(folder.Parent ?? throw new InvalidOperationException(NoRepositoryRoot));
}
