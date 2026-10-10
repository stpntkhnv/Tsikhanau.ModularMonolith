using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class BadRequestTests(PostgresFixture postgresFixture)
{
    private static readonly String[] ProblemMembers = ["type", "title", "status", "code", "errors"];

    public static TheoryData<String> EnvironmentNames => [Environments.Development, Environments.Production];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private String ExplicitSslModeConnectionString =>
        new NpgsqlConnectionStringBuilder(postgresFixture.ConnectionString) { SslMode = SslMode.Disable }
            .ConnectionString;

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Bad_request_answers_400_with_code_request_invalid_and_the_bad_field_under_errors(
        String environmentName)
    {
        await using var testModulesHost = await StartAsync(environmentName);
        using var httpClient = testModulesHost.CreateClient();

        var answers = await SendBadRequestsAsync(httpClient);
        var codesAndErrors = answers.Select(DescribeCodeAndErrors);

        codesAndErrors.ShouldBe(
        [
            "invalid JSON 400 Request.Invalid errors[label]: The value is not valid.",
            "label of the wrong type 400 Request.Invalid errors[label]: The value is not valid.",
            "nickname of the wrong type 400 Request.Invalid errors[nickname]: The value is not valid.",
            "empty body 400 Request.Invalid errors[$]: The request could not be read.",
            "unknown kind in the route 400 Request.Invalid errors[$]: The request could not be read."
        ]);
    }

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Bad_request_answers_in_the_shape_of_a_domain_validation_error(String environmentName)
    {
        await using var testModulesHost = await StartAsync(environmentName);
        using var httpClient = testModulesHost.CreateClient();

        var domainValidationAnswer = await PostPingAsync(httpClient, "empty label", JsonBody("""{"label":""}"""));
        var domainValidationCode = ReadMember(domainValidationAnswer.Body, "code");
        var domainValidationShape = DescribeShape(domainValidationAnswer);
        var answers = await SendBadRequestsAsync(httpClient);
        var shapes = answers.Select(answer => $"{answer.Name}: {DescribeShape(answer)}");
        var expectedShapes = answers.Select(answer => $"{answer.Name}: {domainValidationShape}");

        domainValidationCode.ShouldBe("validation");
        shapes.ShouldBe(expectedShapes);
    }

    [Fact]
    public async Task Missing_label_reaches_the_domain_and_answers_its_validation_error_in_the_same_shape()
    {
        await using var testModulesHost = await StartAsync(Environments.Development);
        using var httpClient = testModulesHost.CreateClient();

        var bodyErrorAnswer = await PostPingAsync(httpClient, "invalid JSON", JsonBody("""{"label":"""));
        var missingLabelAnswer = await PostPingAsync(httpClient, "no label", JsonBody("{}"));
        var missingLabelCodeAndErrors = DescribeCodeAndErrors(missingLabelAnswer);
        var missingLabelMembers = JoinMemberNames(missingLabelAnswer.Body);
        var bodyErrorMembers = JoinMemberNames(bodyErrorAnswer.Body);

        missingLabelCodeAndErrors.ShouldBe("no label 400 validation errors[label]: Label must be 1 to 50 characters");
        missingLabelMembers.ShouldBe(bodyErrorMembers);
    }

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Bad_request_writes_no_log_line_at_warning_or_above(String environmentName)
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var testModulesHost = await StartAsync(
            environmentName,
            builder => builder.Logging.AddProvider(capturingLoggerProvider));

        using var httpClient = testModulesHost.CreateClient();
        capturingLoggerProvider.Clear();

        await SendBadRequestsAsync(httpClient);
        await SendBodiesThatAreNotJsonAsync(httpClient);
        var linesAtWarningOrAbove = capturingLoggerProvider.Entries
            .Where(entry => entry.Level >= LogLevel.Warning)
            .Select(entry => $"{entry.Level} {entry.Category}: {entry.Message}");

        linesAtWarningOrAbove.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EnvironmentNames))]
    public async Task Body_that_is_not_json_answers_415_problem_details(String environmentName)
    {
        await using var testModulesHost = await StartAsync(environmentName);
        using var httpClient = testModulesHost.CreateClient();

        var answers = await SendBodiesThatAreNotJsonAsync(httpClient);
        var problems = answers.Select(DescribeProblem);

        problems.ShouldBe(
        [
            "text/plain body: 415 application/problem+json Unsupported Media Type, type title status",
            "body without a content type: 415 application/problem+json Unsupported Media Type, type title status"
        ]);
    }

    private Task<TestModulesHost> StartAsync(
        String environmentName,
        Action<IHostApplicationBuilder>? configure = null) =>
        TestModulesHost.StartAsync(
            ExplicitSslModeConnectionString,
            configure: configure,
            environmentName: environmentName);

    private static async Task<IReadOnlyList<Answer>> SendBadRequestsAsync(HttpClient httpClient) =>
    [
        await PostPingAsync(httpClient, "invalid JSON", JsonBody("""{"label":""")),
        await PostPingAsync(httpClient, "label of the wrong type", JsonBody("""{"label":5}""")),
        await PostPingAsync(httpClient, "nickname of the wrong type", JsonBody("""{"label":"x","nickname":5}""")),
        await PostPingAsync(httpClient, "empty body", JsonBody(String.Empty)),
        await GetAsync(httpClient, "unknown kind in the route", "/pings/errors/UnknownKind")
    ];

    private static async Task<IReadOnlyList<Answer>> SendBodiesThatAreNotJsonAsync(HttpClient httpClient) =>
    [
        await PostPingAsync(
            httpClient,
            "text/plain body",
            new StringContent("""{"label":"x"}""", Encoding.UTF8, "text/plain")),
        await PostPingAsync(
            httpClient,
            "body without a content type",
            new ByteArrayContent("""{"label":"x"}"""u8.ToArray()))
    ];

    private static StringContent JsonBody(String json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<Answer> PostPingAsync(HttpClient httpClient, String name, HttpContent content)
    {
        using var response = await httpClient.PostAsync("/pings", content, CancellationToken);
        return await ReadAnswerAsync(name, response);
    }

    private static async Task<Answer> GetAsync(HttpClient httpClient, String name, String path)
    {
        using var response = await httpClient.GetAsync(path, CancellationToken);
        return await ReadAnswerAsync(name, response);
    }

    private static async Task<Answer> ReadAnswerAsync(String name, HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var contentType = $"{response.Content.Headers.ContentType?.MediaType}";
        return new Answer(name, (Int32)response.StatusCode, contentType, body);
    }

    private static String DescribeCodeAndErrors(Answer answer) =>
        $"{answer.Name} {answer.StatusCode} {ReadMember(answer.Body, "code")} {DescribeErrors(answer.Body)}";

    private static String DescribeErrors(JsonElement body) =>
        body.TryGetProperty("errors", out var errors)
            ? String.Join(
                "; ",
                errors
                    .EnumerateObject()
                    .Select(field => $"errors[{field.Name}]: {JoinMessages(field.Value)}"))
            : "(no errors)";

    private static String JoinMessages(JsonElement messages) =>
        String.Join(
            ", ",
            messages
                .EnumerateArray()
                .Select(message => message.ToString()));

    private static String DescribeShape(Answer answer) =>
        String.Join(
            ", ",
            $"{answer.StatusCode} {answer.ContentType}",
            $"type {ReadMember(answer.Body, "type")}",
            $"title {ReadMember(answer.Body, "title")}",
            $"members {JoinMemberNames(answer.Body)}");

    private static String JoinMemberNames(JsonElement body) =>
        String.Join(
            " ",
            body
                .EnumerateObject()
                .Select(member => member.Name)
                .Order(StringComparer.Ordinal));

    private static String DescribeProblem(Answer answer) =>
        String.Join(
            ", ",
            $"{answer.Name}: {answer.StatusCode} {answer.ContentType} {ReadMember(answer.Body, "title")}",
            JoinProblemMembers(answer.Body));

    private static String JoinProblemMembers(JsonElement body) =>
        String.Join(" ", ProblemMembers.Where(member => body.TryGetProperty(member, out _)));

    private static String ReadMember(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : $"(no {name})";

    private sealed record Answer(String Name, Int32 StatusCode, String ContentType, JsonElement Body);
}
