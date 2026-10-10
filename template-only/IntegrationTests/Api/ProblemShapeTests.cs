using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class ProblemShapeTests(PostgresFixture postgresFixture)
{
    private const String Alice = "alice";

    private const String NoAcceptHeader = "(none)";

    private const Int32 ShownBodyLength = 40;

    private const String ProblemJson = "application/problem+json";

    private const String ValidationTitle = "One or more validation errors occurred.";

    private const String ServerErrorTitle = "An error occurred while processing your request.";

    private const String BadBodyMembers = "code errors status title traceId type";

    private const String ServerError =
        $"500 {ProblemJson} {ServerErrorTitle}, code Server.Unexpected, members code status title traceId type";

    private static readonly String[] AcceptHeaders = [NoAcceptHeader, "text/html", "application/xml"];

    private static readonly String[] ExpectedProblems =
    [
        $"bad body: 400 {ProblemJson} {ValidationTitle}, code Request.Invalid, members {BadBodyMembers}",
        $"no caller: 401 {ProblemJson} Unauthorized, code (no code), members status title traceId type",
        $"helper 500: {ServerError}",
        $"unhandled exception: {ServerError}"
    ];

    public static TheoryData<String> EnvironmentNames => [Environments.Development, Environments.Production];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private String ExplicitSslModeConnectionString =>
        new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString) { SslMode = SslMode.Disable }
            .ConnectionString;

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Error_answers_the_same_problem_json_whatever_the_accept_header(String environmentName)
    {
        await using var testModulesHost = await StartAsync(environmentName);
        var pingId = await testModulesHost.CreatePingAsync("thrown");
        using var httpClient = testModulesHost.CreateClient();

        List<String> problems = [];
        foreach (var accept in AcceptHeaders)
        {
            problems.AddRange(await DescribeProblemsAsync(httpClient, accept, pingId));
        }

        var expectedProblems = AcceptHeaders.SelectMany(
            accept => ExpectedProblems.Select(problem => $"Accept {accept}, {problem}"));

        problems.ShouldBe(expectedProblems);
    }

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Authentication_scheme_that_throws_answers_the_generic_500_without_internals(
        String environmentName)
    {
        await using var testModulesHost = await StartAsync(environmentName);
        var pingId = await testModulesHost.CreatePingAsync("behind a failing scheme");
        using var httpClient = testModulesHost.CreateClient();

        var request = Get($"/pings/{pingId}");
        request.Headers.Add(TestCallerAuthenticationHandler.FailingSchemeHeader, "yes");
        var answer = await SendAsync(httpClient, "failing scheme", request);
        var problem = DescribeProblem(answer);

        problem.ShouldBe($"failing scheme: {ServerError}");
        answer.Body.ShouldNotContain(TestCallerAuthenticationHandler.SchemeFailure);
    }

    private Task<TestModulesHost> StartAsync(String environmentName) =>
        TestModulesHost.StartAsync(
            ExplicitSslModeConnectionString,
            environmentName: environmentName,
            requiresAuthentication: true);

    private static async Task<IReadOnlyList<String>> DescribeProblemsAsync(
        HttpClient httpClient,
        String accept,
        Guid pingId)
    {
        IReadOnlyList<Answer> answers =
        [
            await SendAsync(httpClient, "bad body", Accepting(accept, As(Alice, PostPing("""{"label":""")))),
            await SendAsync(httpClient, "no caller", Accepting(accept, Get($"/pings/{pingId}"))),
            await SendAsync(httpClient, "helper 500", Accepting(accept, As(Alice, Get("/pings/errors/Unexpected")))),
            await SendAsync(
                httpClient,
                "unhandled exception",
                Accepting(accept, As(Alice, new HttpRequestMessage(HttpMethod.Post, $"/pings/{pingId}/throw"))))
        ];

        return answers
            .Select(answer => $"Accept {accept}, {DescribeProblem(answer)}")
            .ToList();
    }

    private static HttpRequestMessage Get(String path) => new(HttpMethod.Get, path);

    private static HttpRequestMessage PostPing(String json) =>
        new(HttpMethod.Post, "/pings") { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpRequestMessage As(String callerId, HttpRequestMessage request)
    {
        request.Headers.Add(TestCallerAuthenticationHandler.CallerHeader, callerId);
        return request;
    }

    private static HttpRequestMessage Accepting(String accept, HttpRequestMessage request)
    {
        if (accept != NoAcceptHeader)
        {
            request.Headers.Accept.ParseAdd(accept);
        }

        return request;
    }

    private static async Task<Answer> SendAsync(HttpClient httpClient, String name, HttpRequestMessage request)
    {
        using var sentRequest = request;
        using var response = await httpClient.SendAsync(sentRequest, CancellationToken);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        var contentType = $"{response.Content.Headers.ContentType?.MediaType}";
        return new Answer(name, (Int32)response.StatusCode, contentType, body);
    }

    private static String DescribeProblem(Answer answer) =>
        $"{answer.Name}: {answer.StatusCode} {answer.ContentType} {DescribeProblemBody(answer.Body)}";

    private static String DescribeProblemBody(String body) =>
        body.StartsWith('{')
            ? DescribeProblemDetails(JsonSerializer.Deserialize<JsonElement>(body))
            : $"(not a problem: {ShowStart(body)})";

    private static String ShowStart(String body)
    {
        var trimmedBody = body.Trim();
        return trimmedBody[..Math.Min(trimmedBody.Length, ShownBodyLength)];
    }

    private static String DescribeProblemDetails(JsonElement problemDetails) =>
        String.Join(
            ", ",
            ReadMember(problemDetails, "title"),
            $"code {ReadMember(problemDetails, "code")}",
            $"members {JoinMemberNames(problemDetails)}");

    private static String JoinMemberNames(JsonElement problemDetails) =>
        String.Join(
            " ",
            problemDetails
                .EnumerateObject()
                .Select(member => member.Name)
                .Order(StringComparer.Ordinal));

    private static String ReadMember(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : $"(no {name})";

    private sealed record Answer(String Name, Int32 StatusCode, String ContentType, String Body);
}
