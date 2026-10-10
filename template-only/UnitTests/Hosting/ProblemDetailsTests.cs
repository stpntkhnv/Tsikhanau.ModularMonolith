using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Hosting;

namespace Tsikhanau.Monolith.UnitTests.Hosting;

public sealed class ProblemDetailsTests
{
    private const String Secret = "the connection string of the payments database";

    private const String SecretTitle = "Payments database refused the login";

    private const String SecretType = "https://payments.internal/errors/login";

    private const String SecretInstance = "/payments/db-01";

    private const String SecretExtension = "connectionString";

    private const String ProblemJson = "application/problem+json";

    private const String BadBody = """{"name":1}""";

    private const String ServerErrorTitle = "An error occurred while processing your request.";

    private const String ServerErrorType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";

    private const String ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private const String NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";

    private const String AppExtension = "tenant";

    private const String AppTenant = "acme";

    private const String ServerError =
        $"500 {ProblemJson} {ServerErrorTitle}, type {ServerErrorType}, code Server.Unexpected, "
        + "members code status title traceId type";

    private const String Conflict =
        $"409 {ProblemJson} Conflict, type {ConflictType}, code (no code), members status title traceId type";

    private const String ServerErrorInSnakeCase =
        $"500 {ProblemJson} {ServerErrorTitle}, type {ServerErrorType}, code Server.Unexpected, "
        + "members code status title trace_id type";

    private const String NotFoundInSnakeCase =
        $"404 {ProblemJson} Not Found, type {NotFoundType}, code (no code), members status title trace_id type";

    private const String BadBodyWithTheAppExtension =
        $$"""400 code Request.Invalid, errors {"name":["The value is not valid."]}, {{AppExtension}} {{AppTenant}}, """
        + "members code errors status tenant title traceId type";

    private const String ServerErrorWithoutTheAppExtension =
        $"500 code Server.Unexpected, errors (no errors), {AppExtension} (no {AppExtension}), "
        + "members code status title traceId type";

    private const String NotFoundWithTheAppExtension =
        $"404 code (no code), errors (no errors), {AppExtension} {AppTenant}, members status tenant title traceId type";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Server_error_answers_code_server_unexpected_without_its_detail_for_every_client()
    {
        await using var app = await StartProbeAppAsync(
            probeApp => probeApp.MapGet("/probe", () => TypedResults.Problem(Secret, statusCode: 500)));

        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeAsync(httpClient, "no Accept header", new HttpRequestMessage(HttpMethod.Get, "/probe")),
            await DescribeAsync(httpClient, "Accept text/html", Accepting("text/html", "/probe"))
        ];

        answers.ShouldBe(
        [
            $"no Accept header: {ServerError}",
            $"Accept text/html: {ServerError}"
        ]);
    }

    [Fact]
    public async Task Server_error_keeps_nothing_the_endpoint_set_but_its_status_for_every_client()
    {
        await using var app = await StartProbeAppAsync(
            probeApp => probeApp.MapGet("/probe", WriteServerErrorWithEveryMember));

        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeAsync(httpClient, "no Accept header", new HttpRequestMessage(HttpMethod.Get, "/probe")),
            await DescribeAsync(httpClient, "Accept text/html", Accepting("text/html", "/probe"))
        ];

        answers.ShouldBe(
        [
            $"no Accept header: {ServerError}",
            $"Accept text/html: {ServerError}"
        ]);
    }

    [Fact]
    public async Task Problem_written_without_a_status_takes_the_status_of_the_response_for_every_client()
    {
        await using var app = await StartProbeAppAsync(
            probeApp => probeApp.MapGet("/probe", WriteConflictWithoutAStatusAsync));

        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeAsync(httpClient, "no Accept header", new HttpRequestMessage(HttpMethod.Get, "/probe")),
            await DescribeAsync(httpClient, "Accept text/html", Accepting("text/html", "/probe"))
        ];

        answers.ShouldBe([$"no Accept header: {Conflict}", $"Accept text/html: {Conflict}"]);
    }

    [Fact]
    public async Task App_that_customizes_problems_keeps_the_core_problem_shape_for_every_client()
    {
        await using var app = await StartProbeAppAsync(CustomizeProblemsLikeAnApp, MapCustomizationProbes);
        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeCustomizedAsync(httpClient, "bad body", PostJson("/probe", BadBody)),
            await DescribeCustomizedAsync(
                httpClient,
                "bad body, Accept text/html",
                Accepting("text/html", PostJson("/probe", BadBody))),
            await DescribeCustomizedAsync(httpClient, "server error", GetProbe("/probe")),
            await DescribeCustomizedAsync(
                httpClient,
                "server error, Accept text/html",
                Accepting("text/html", "/probe")),
            await DescribeCustomizedAsync(httpClient, "unknown route", GetProbe("/unknown")),
            await DescribeCustomizedAsync(
                httpClient,
                "unknown route, Accept text/html",
                Accepting("text/html", "/unknown"))
        ];

        answers.ShouldBe(
        [
            $"bad body: {BadBodyWithTheAppExtension}",
            $"bad body, Accept text/html: {BadBodyWithTheAppExtension}",
            $"server error: {ServerErrorWithoutTheAppExtension}",
            $"server error, Accept text/html: {ServerErrorWithoutTheAppExtension}",
            $"unknown route: {NotFoundWithTheAppExtension}",
            $"unknown route, Accept text/html: {NotFoundWithTheAppExtension}"
        ]);
    }

    [Fact]
    public async Task Trace_id_takes_the_naming_policy_of_the_app_and_stays_on_a_server_error_for_every_client()
    {
        await using var app = await StartProbeAppAsync(
            NameJsonMembersInSnakeCase,
            probeApp => probeApp.MapGet("/probe", WriteServerErrorWithEveryMember));

        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeAsync(httpClient, "server error", GetProbe("/probe")),
            await DescribeAsync(httpClient, "server error, Accept text/html", Accepting("text/html", "/probe")),
            await DescribeAsync(httpClient, "unknown route", GetProbe("/unknown")),
            await DescribeAsync(httpClient, "unknown route, Accept text/html", Accepting("text/html", "/unknown"))
        ];

        answers.ShouldBe(
        [
            $"server error: {ServerErrorInSnakeCase}",
            $"server error, Accept text/html: {ServerErrorInSnakeCase}",
            $"unknown route: {NotFoundInSnakeCase}",
            $"unknown route, Accept text/html: {NotFoundInSnakeCase}"
        ]);
    }

    [Fact]
    public async Task Binding_failure_names_the_parts_of_the_request_it_can_come_from()
    {
        await using var app = await StartProbeAppAsync(MapBindingProbes);
        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeErrorsAsync(httpClient, "bad query value, no body taken", GetProbe("/probe?count=many")),
            await DescribeErrorsAsync(httpClient, "bad query value next to a body", PostProbe("""{"name":"probe"}""")),
            await DescribeErrorsAsync(httpClient, "null body", PostProbe("null", count: "1")),
            await DescribeErrorsAsync(httpClient, "empty body", PostProbe(String.Empty, count: "1"))
        ];

        answers.ShouldBe(
        [
            "bad query value, no body taken: 400 Request.Invalid errors[$]: A route or query value is not valid.",
            "bad query value next to a body: 400 Request.Invalid errors[$]: "
            + "The body or a route or query value is not valid.",
            "null body: 400 Request.Invalid errors[$]: The body or a route or query value is not valid.",
            "empty body: 400 Request.Invalid errors[$]: The request could not be read."
        ]);
    }

    private static Task<WebApplication> StartProbeAppAsync(Action<WebApplication> mapProbe) =>
        StartProbeAppAsync(_ => { }, mapProbe);

    private static async Task<WebApplication> StartProbeAppAsync(
        Action<WebApplicationBuilder> configureApp,
        Action<WebApplication> mapProbe)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();
        configureApp(builder);

        var app = builder.Build();
        app.UseServiceDefaults();
        mapProbe(app);
        await app.StartAsync(CancellationToken);
        return app;
    }

    private static void MapBindingProbes(WebApplication app)
    {
        app.MapGet("/probe", (Int32 count) => TypedResults.NoContent());
        app.MapPost("/probe", (ProbeRequest request, Int32 count) => TypedResults.NoContent());
    }

    private static void MapCustomizationProbes(WebApplication app)
    {
        app.MapGet("/probe", WriteServerErrorWithEveryMember);
        app.MapPost("/probe", (ProbeRequest request) => TypedResults.NoContent());
    }

    private static void CustomizeProblemsLikeAnApp(WebApplicationBuilder builder) =>
        builder.Services.AddProblemDetails(
            problemDetailsOptions => problemDetailsOptions.CustomizeProblemDetails = AddTheTenant);

    private static void AddTheTenant(ProblemDetailsContext context) =>
        context.ProblemDetails.Extensions[AppExtension] = AppTenant;

    private static void NameJsonMembersInSnakeCase(WebApplicationBuilder builder) =>
        builder.Services.ConfigureHttpJsonOptions(
            jsonOptions => jsonOptions.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

    private static ProblemHttpResult WriteServerErrorWithEveryMember() =>
        TypedResults.Problem(
            detail: Secret,
            instance: SecretInstance,
            statusCode: StatusCodes.Status500InternalServerError,
            title: SecretTitle,
            type: SecretType,
            extensions: new Dictionary<String, Object?> { [SecretExtension] = Secret });

    private static ValueTask WriteConflictWithoutAStatusAsync(
        HttpContext httpContext,
        IProblemDetailsService problemDetailsService)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext });
    }

    private static HttpRequestMessage Accepting(String accept, String path) =>
        Accepting(accept, new HttpRequestMessage(HttpMethod.Get, path));

    private static HttpRequestMessage Accepting(String accept, HttpRequestMessage request)
    {
        request.Headers.Accept.ParseAdd(accept);
        return request;
    }

    private static HttpRequestMessage GetProbe(String path) => new(HttpMethod.Get, path);

    private static HttpRequestMessage PostProbe(String json, String count = "many") =>
        PostJson($"/probe?count={count}", json);

    private static HttpRequestMessage PostJson(String path, String json) =>
        new(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static async Task<String> DescribeAsync(HttpClient httpClient, String name, HttpRequestMessage request)
    {
        using var sentRequest = request;
        using var response = await httpClient.SendAsync(sentRequest, CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var answer = $"{(Int32)response.StatusCode} {contentType} {ReadMember(body, "title")}";
        var typeAndCode = $"type {ReadMember(body, "type")}, code {ReadMember(body, "code")}";
        return $"{name}: {answer}, {typeAndCode}, members {JoinMemberNames(body)}";
    }

    private static async Task<String> DescribeCustomizedAsync(
        HttpClient httpClient,
        String name,
        HttpRequestMessage request)
    {
        using var sentRequest = request;
        using var response = await httpClient.SendAsync(sentRequest, CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var codeAndErrors = $"code {ReadMember(body, "code")}, errors {ReadMember(body, "errors")}";
        var appExtension = $"{AppExtension} {ReadMember(body, AppExtension)}";
        return $"{name}: {(Int32)response.StatusCode} {codeAndErrors}, {appExtension}, members {JoinMemberNames(body)}";
    }

    private static async Task<String> DescribeErrorsAsync(
        HttpClient httpClient,
        String name,
        HttpRequestMessage request)
    {
        using var sentRequest = request;
        using var response = await httpClient.SendAsync(sentRequest, CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        return $"{name}: {(Int32)response.StatusCode} {ReadMember(body, "code")} {DescribeErrors(body)}";
    }

    private static String DescribeErrors(JsonElement body) =>
        String.Join(
            "; ",
            body
                .GetProperty("errors")
                .EnumerateObject()
                .Select(field => $"errors[{field.Name}]: {String.Join(", ", field.Value.EnumerateArray())}"));

    private static String JoinMemberNames(JsonElement body) =>
        String.Join(
            " ",
            body
                .EnumerateObject()
                .Select(member => member.Name)
                .Order(StringComparer.Ordinal));

    private static String ReadMember(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : $"(no {name})";

    private sealed record ProbeRequest(String Name);
}
