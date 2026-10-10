using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class CallerTests(PostgresFixture postgresFixture)
{
    private const String Alice = "alice";

    private const String BlankId = " ";

    private const String CountPingsSql = "SELECT count(*)::int FROM pings.pings";

    private static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(30);

    private static readonly String[] ProblemMembers = ["type", "title", "status", "code"];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Module_endpoints_answer_401_problem_details_without_a_caller()
    {
        await using var testModulesHost = await StartAsync();
        var pingId = await testModulesHost.CreatePingAsync("closed");
        using var httpClient = testModulesHost.CreateClient();

        IReadOnlyList<Answer> answers =
        [
            await SendAsync(httpClient, "GET without a caller", GetPing(pingId)),
            await SendAsync(httpClient, "POST without a caller", PostPing("anonymous"))
        ];

        var problems = answers.Select(DescribeProblem);
        var pingCount = await testModulesHost.Database.ExecuteScalarAsync<Int32>(CountPingsSql);

        problems.ShouldBe(
        [
            "GET without a caller: 401 application/problem+json Unauthorized, type title status",
            "POST without a caller: 401 application/problem+json Unauthorized, type title status"
        ]);

        pingCount.ShouldBe(1);
    }

    [Fact]
    public async Task Health_alive_and_openapi_stay_open_without_a_caller()
    {
        await using var testModulesHost = await StartAsync();
        using var httpClient = testModulesHost.CreateClient();

        IReadOnlyList<Answer> answers =
        [
            await SendAsync(httpClient, "GET /health", Get("/health")),
            await SendAsync(httpClient, "GET /alive", Get("/alive")),
            await SendAsync(httpClient, "GET /openapi/v1.json", Get("/openapi/v1.json"))
        ];

        var statuses = answers.Select(DescribeStatus);

        statuses.ShouldBe(["GET /health: 200", "GET /alive: 200", "GET /openapi/v1.json: 200"]);
    }

    [Fact]
    public async Task Authenticated_request_without_a_caller_id_answers_403_and_reaches_no_handler()
    {
        await using var testModulesHost = await StartAsync();
        var pingId = await testModulesHost.CreatePingAsync("forbidden");
        using var httpClient = testModulesHost.CreateClient();

        IReadOnlyList<Answer> answers =
        [
            await SendAsync(httpClient, "GET without an id", WithoutCallerId(GetPing(pingId))),
            await SendAsync(httpClient, "POST without an id", WithoutCallerId(PostPing("without an id"))),
            await SendAsync(httpClient, "GET with a blank id", As(BlankId, GetPing(pingId))),
            await SendAsync(httpClient, "POST with a blank id", As(BlankId, PostPing("with a blank id")))
        ];

        var problems = answers.Select(DescribeProblem);
        var pingCount = await testModulesHost.Database.ExecuteScalarAsync<Int32>(CountPingsSql);

        problems.ShouldBe(
        [
            "GET without an id: 403 application/problem+json Forbidden, type title status",
            "POST without an id: 403 application/problem+json Forbidden, type title status",
            "GET with a blank id: 403 application/problem+json Forbidden, type title status",
            "POST with a blank id: 403 application/problem+json Forbidden, type title status"
        ]);

        pingCount.ShouldBe(1);
    }

    [Fact]
    public async Task Caller_reaches_the_module_endpoints()
    {
        await using var testModulesHost = await StartAsync();
        var pingId = await testModulesHost.CreatePingAsync("open");
        using var httpClient = testModulesHost.CreateClient();

        IReadOnlyList<Answer> answers =
        [
            await SendAsync(httpClient, "GET as alice", As(Alice, GetPing(pingId))),
            await SendAsync(httpClient, "POST as alice", As(Alice, PostPing("posted by alice")))
        ];

        var statuses = answers.Select(DescribeStatus);

        statuses.ShouldBe(["GET as alice: 200", "POST as alice: 201"]);
    }

    [Fact]
    public async Task Handler_behind_the_facade_sees_the_caller_and_the_queue_handler_sees_nobody()
    {
        await using var testModulesHost = await StartAsync();
        using var httpClient = testModulesHost.CreateClient();

        var answer = await SendAsync(httpClient, "POST as alice", As(Alice, PostPing("created by alice")));
        await testModulesHost.ProcessQueuesAsync();

        testModulesHost.PongHooks.ReceivedCallers
            .ShouldHaveSingleItem()
            .ShouldBe(new PingCreatedCallers(ReadPingId(answer), Alice, Optional<String>.None()));
    }

    [Fact]
    public async Task Background_handler_sees_nobody_while_the_request_is_still_open()
    {
        var requestHold = new RequestHold();
        var pongHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            runInBackground: true,
            configure: builder => builder.Services.AddSingleton<IStartupFilter>(requestHold),
            requiresAuthentication: true);

        testModulesHost.PongHooks.BeforeHandle = () =>
        {
            pongHandled.TrySetResult();
            return Task.CompletedTask;
        };

        using var httpClient = testModulesHost.CreateClient();

        var answerTask = SendAsync(httpClient, "POST as alice", As(Alice, PostPing("created in the background")));
        await pongHandled.Task.WaitAsync(MaxHold, CancellationToken);
        var isRequestOpenWhilePongIsHandled = !answerTask.IsCompleted;
        requestHold.Release();
        var answer = await answerTask;

        isRequestOpenWhilePongIsHandled.ShouldBeTrue();
        testModulesHost.PongHooks.ReceivedCallers
            .ShouldHaveSingleItem()
            .ShouldBe(new PingCreatedCallers(ReadPingId(answer), Alice, Optional<String>.None()));
    }

    [Fact]
    public async Task Ping_created_through_the_facade_from_test_code_has_no_creator()
    {
        await using var testModulesHost = await StartAsync();
        var pingId = await testModulesHost.CreatePingAsync("from test code");

        await testModulesHost.ProcessQueuesAsync();

        testModulesHost.PongHooks.ReceivedCallers
            .ShouldHaveSingleItem()
            .ShouldBe(new PingCreatedCallers(pingId, Optional<String>.None(), Optional<String>.None()));
    }

    private Task<TestModulesHost> StartAsync() =>
        TestModulesHost.StartAsync(postgresFixture, requiresAuthentication: true);

    private static HttpRequestMessage Get(String path) => new(HttpMethod.Get, path);

    private static HttpRequestMessage GetPing(Guid pingId) => Get($"/pings/{pingId}");

    private static HttpRequestMessage PostPing(String label) =>
        new(HttpMethod.Post, "/pings") { Content = JsonContent.Create(new { label }) };

    private static HttpRequestMessage As(String callerId, HttpRequestMessage request)
    {
        request.Headers.Add(TestCallerAuthenticationHandler.CallerHeader, callerId);
        return request;
    }

    private static HttpRequestMessage WithoutCallerId(HttpRequestMessage request)
    {
        request.Headers.Add(TestCallerAuthenticationHandler.CallerWithoutIdHeader, "yes");
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

    private static Guid ReadPingId(Answer answer) => JsonSerializer.Deserialize<Guid>(answer.Body);

    private static String DescribeStatus(Answer answer) => $"{answer.Name}: {answer.StatusCode}";

    private static String DescribeProblem(Answer answer) =>
        $"{answer.Name}: {answer.StatusCode} {answer.ContentType} {DescribeProblemBody(answer.Body)}";

    private static String DescribeProblemBody(String body) =>
        body.StartsWith('{')
            ? DescribeProblemDetails(JsonSerializer.Deserialize<JsonElement>(body))
            : $"(not a problem: {body})";

    private static String DescribeProblemDetails(JsonElement problemDetails) =>
        $"{ReadMember(problemDetails, "title")}, {JoinProblemMembers(problemDetails)}";

    private static String JoinProblemMembers(JsonElement problemDetails) =>
        String.Join(" ", ProblemMembers.Where(member => problemDetails.TryGetProperty(member, out _)));

    private static String ReadMember(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : $"(no {name})";

    private sealed record Answer(String Name, Int32 StatusCode, String ContentType, String Body);

    private sealed class RequestHold : IStartupFilter
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _released.TrySetResult();

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(HoldAsync);
                next(app);
            };

        private async Task HoldAsync(HttpContext httpContext, RequestDelegate next)
        {
            await next(httpContext);
            await _released.Task.WaitAsync(MaxHold);
        }
    }
}
