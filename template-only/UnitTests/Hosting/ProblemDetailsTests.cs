using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Hosting;

namespace Tsikhanau.Monolith.UnitTests.Hosting;

public sealed class ProblemDetailsTests
{
    private const String Secret = "the connection string of the payments database";

    private const String ProblemJson = "application/problem+json";

    private const String ServerErrorTitle = "An error occurred while processing your request.";

    private const String ServerError =
        $"500 {ProblemJson} {ServerErrorTitle}, code Server.Unexpected, members code status title traceId type";

    private const String Conflict = $"409 {ProblemJson} Conflict, code (no code), members status title traceId type";

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
    public async Task Binding_failure_outside_the_body_answers_its_own_message()
    {
        await using var app = await StartProbeAppAsync(
            probeApp => probeApp.MapPost("/probe", (ProbeRequest request, Int32 count) => TypedResults.NoContent()));

        using var httpClient = app.GetTestClient();

        IReadOnlyList<String> answers =
        [
            await DescribeErrorsAsync(httpClient, "bad query value", "/probe?count=many", """{"name":"probe"}"""),
            await DescribeErrorsAsync(httpClient, "empty body", "/probe?count=1", String.Empty)
        ];

        answers.ShouldBe(
        [
            "bad query value: 400 Request.Invalid errors[$]: A route or query value is not valid.",
            "empty body: 400 Request.Invalid errors[$]: The request could not be read."
        ]);
    }

    private static async Task<WebApplication> StartProbeAppAsync(Action<WebApplication> mapProbe)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();

        var app = builder.Build();
        app.UseServiceDefaults();
        mapProbe(app);
        await app.StartAsync(CancellationToken);
        return app;
    }

    private static ValueTask WriteConflictWithoutAStatusAsync(
        HttpContext httpContext,
        IProblemDetailsService problemDetailsService)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext });
    }

    private static HttpRequestMessage Accepting(String accept, String path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd(accept);
        return request;
    }

    private static async Task<String> DescribeAsync(HttpClient httpClient, String name, HttpRequestMessage request)
    {
        using var sentRequest = request;
        using var response = await httpClient.SendAsync(sentRequest, CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var answer = $"{(Int32)response.StatusCode} {contentType} {ReadMember(body, "title")}";
        return $"{name}: {answer}, code {ReadMember(body, "code")}, members {JoinMemberNames(body)}";
    }

    private static async Task<String> DescribeErrorsAsync(HttpClient httpClient, String name, String path, String json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(path, content, CancellationToken);
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
