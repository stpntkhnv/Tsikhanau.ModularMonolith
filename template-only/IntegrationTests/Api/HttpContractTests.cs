using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class HttpContractTests(PostgresFixture postgresFixture)
{
    private const String GetPingOperation = "GET /pings/{pingId}";
    private const String ProduceErrorOperation = "GET /pings/errors/{kind}";
    private const String ThrowAfterTouchOperation = "POST /pings/{pingId}/throw";
    private const String ProblemDetailsSchema = "#/components/schemas/ProblemDetails";
    private const String ValidationProblemDetailsSchema = "#/components/schemas/HttpValidationProblemDetails";
    private const String ProblemJson = "application/problem+json";

    private static readonly String[] ModulePathPrefixes = ["/pings", "/pongs"];

    private static readonly String[] MessageSuffixes = ["Command", "Query"];

    private static readonly String[] OperationMethods = ["get", "put", "post", "delete", "patch"];

    private static readonly String[] ProblemMembers = ["type", "title", "status", "code", "errors"];

    private static readonly String[] DescribedErrorResponses =
    [
        $"400 {ProblemJson} {ValidationProblemDetailsSchema}",
        $"401 {ProblemJson} {ProblemDetailsSchema}",
        $"403 {ProblemJson} {ProblemDetailsSchema}",
        $"404 {ProblemJson} {ProblemDetailsSchema}",
        $"409 {ProblemJson} {ProblemDetailsSchema}",
        $"422 {ProblemJson} {ProblemDetailsSchema}",
        $"500 {ProblemJson} {ProblemDetailsSchema}"
    ];

    private static readonly Dictionary<String, String> MembersBySchema = new(StringComparer.Ordinal)
    {
        [ProblemDetailsSchema] = "type title status code",
        [ValidationProblemDetailsSchema] = "type title status code errors"
    };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_module_operation_describes_the_error_responses()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var operations = ReadModuleOperations(document);
        var operationNames = operations.Select(operation => operation.Name);
        var errorResponses = operations.SelectMany(DescribeErrorResponses);
        var expectedErrorResponses = operations.SelectMany(
            operation => DescribedErrorResponses.Select(response => $"{operation.Name} {response}"));

        operationNames.ShouldBe(
            [
                GetPingOperation,
                "POST /pings",
                "POST /pings/{pingId}/touch",
                ProduceErrorOperation,
                ThrowAfterTouchOperation,
                "GET /pongs/{pongId}/source"
            ],
            ignoreOrder: true);

        errorResponses.ShouldBe(expectedErrorResponses, ignoreOrder: true);
    }

    [Fact]
    public async Task Every_module_operation_describes_its_success_response()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var successResponses = ReadModuleOperations(document)
            .SelectMany(DescribeSuccessResponses);

        successResponses.ShouldBe(
            [
                "GET /pings/{pingId} 200 application/json #/components/schemas/PingDto",
                """POST /pings 201 application/json {"type":"string","format":"uuid"}""",
                "POST /pings/{pingId}/touch 204 no content",
                "GET /pings/errors/{kind} 204 no content",
                "POST /pings/{pingId}/throw 204 no content",
                "GET /pongs/{pongId}/source 200 application/json #/components/schemas/PongSourceDto"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Create_ping_operation_takes_its_request_record_as_the_body()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var requestBodies = ReadModuleOperations(document)
            .SelectMany(DescribeRequestBody);

        requestBodies.ShouldBe(["POST /pings application/json #/components/schemas/CreatePingRequest"]);
    }

    [Fact]
    public async Task No_schema_is_named_after_a_command_a_query_or_an_optional()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var schemaNames = ReadSchemaNames(document);
        var namesOfMessagesOrOptionals = schemaNames.Where(IsNamedAfterAMessageOrAnOptional);

        schemaNames.ShouldContain("CreatePingRequest");
        namesOfMessagesOrOptionals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Each_error_answers_with_the_status_code_and_detail_of_its_kind()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var answers = await ProduceEveryErrorAsync(testModulesHost, httpClient);
        var statusCodesAndDetails = answers.Select(DescribeStatusCodeAndDetail);

        statusCodesAndDetails.ShouldBe(
        [
            "ValidationWithFields 400 validation (no detail)",
            "ValidationWithoutField 400 Ping.BadShape (no detail)",
            "AggregateOfTwoValidations 400 aggregate (no detail)",
            "AggregateOfFieldAndFieldlessValidations 400 aggregate (no detail)",
            "Unauthorized 401 Ping.Unauthorized Caller is not signed in",
            "Forbidden 403 Ping.Forbidden Caller may not see this ping",
            "NotFound 404 Ping.NotFound Ping was not found",
            "Conflict 409 Ping.TooManyTouches Ping cannot be touched more than 3 times",
            "Failure 422 Ping.NotProcessed Ping could not be processed",
            "Unexpected 500 Server.Unexpected (no detail)",
            "AggregateHoldingUnexpected 500 Server.Unexpected (no detail)",
            "UnhandledException 500 Server.Unexpected (no detail)"
        ]);
    }

    [Fact]
    public async Task Validation_errors_answer_with_every_message_under_its_field()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var answers = await ProduceEveryErrorAsync(testModulesHost, httpClient);
        var fieldErrors = answers
            .Where(answer => answer.StatusCode == StatusCodes.Status400BadRequest)
            .SelectMany(DescribeFieldErrors);

        fieldErrors.ShouldBe(
        [
            "ValidationWithFields errors[label]: Label is too long, Label has spaces",
            "ValidationWithFields errors[nickname]: Nickname is empty",
            "ValidationWithoutField errors[]: Ping has a bad shape",
            "AggregateOfTwoValidations errors[label]: Label is too long",
            "AggregateOfTwoValidations errors[nickname]: Nickname is empty",
            "AggregateOfFieldAndFieldlessValidations errors[label]: Label is too long",
            "AggregateOfFieldAndFieldlessValidations errors[]: Ping has a bad shape"
        ]);
    }

    [Fact]
    public async Task Every_error_answer_is_described_by_its_operation()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();
        var document = await ReadDocumentAsync(httpClient);

        var answers = await ProduceEveryErrorAsync(testModulesHost, httpClient);
        var answerShapes = answers.Select(DescribeShape);
        var describedShapes = answers.Select(answer => DescribeDocumentedShape(document, answer));

        answerShapes.ShouldBe(describedShapes);
    }

    [Fact]
    public async Task Every_module_endpoint_has_a_unique_route_name_of_its_module_and_scenario()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);

        var routeEndpoints = ReadRouteEndpoints(testModulesHost);
        var moduleEndpoints = routeEndpoints
            .Where(IsModuleEndpoint)
            .ToList();

        var endpointsWithoutAScenarioName = moduleEndpoints
            .Where(endpoint => !HasScenarioNameOfItsModule(endpoint))
            .Select(endpoint => endpoint.DisplayName);

        var routeNames = routeEndpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName)
            .OfType<String>();

        moduleEndpoints.ShouldNotBeEmpty();
        endpointsWithoutAScenarioName.ShouldBeEmpty();
        routeNames.ShouldBeUnique();
    }

    [Fact]
    public async Task Route_name_is_the_operation_id()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var getPingOperation = GetOperation(document, GetPingOperation);
        var operationId = ReadMember(getPingOperation.Element, "operationId");

        operationId.ShouldBe("Pings.GetPing");
    }

    private static Task<JsonElement> ReadDocumentAsync(HttpClient httpClient) =>
        httpClient.GetFromJsonAsync<JsonElement>("/openapi/v1.json", CancellationToken);

    private static IReadOnlyList<DocumentOperation> ReadModuleOperations(JsonElement document) =>
        document
            .GetProperty("paths")
            .EnumerateObject()
            .Where(path => IsModulePath(path.Name))
            .SelectMany(ReadOperations)
            .ToList();

    private static Boolean IsModulePath(String path) =>
        ModulePathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static IEnumerable<DocumentOperation> ReadOperations(JsonProperty path) =>
        path.Value
            .EnumerateObject()
            .Where(operation => OperationMethods.Contains(operation.Name))
            .Select(operation => new DocumentOperation(BuildOperationName(operation.Name, path.Name), operation.Value));

    private static String BuildOperationName(String method, String path) => $"{method.ToUpperInvariant()} {path}";

    private static DocumentOperation GetOperation(JsonElement document, String name) =>
        ReadModuleOperations(document)
            .Single(operation => operation.Name == name);

    private static IEnumerable<String> DescribeErrorResponses(DocumentOperation operation) =>
        DescribeResponses(operation, statusCode => statusCode >= StatusCodes.Status400BadRequest);

    private static IEnumerable<String> DescribeSuccessResponses(DocumentOperation operation) =>
        DescribeResponses(operation, statusCode => statusCode < StatusCodes.Status400BadRequest);

    private static IEnumerable<String> DescribeResponses(
        DocumentOperation operation,
        Func<Int32, Boolean> isIncluded) =>
        operation.Responses
            .EnumerateObject()
            .Where(response => isIncluded(Int32.Parse(response.Name, CultureInfo.InvariantCulture)))
            .Select(response => $"{operation.Name} {response.Name} {DescribeContent(response.Value)}");

    private static String DescribeContent(JsonElement response) =>
        response.TryGetProperty("content", out var content)
            ? JoinMediaTypes(content, DescribeMediaType)
            : "no content";

    private static String JoinMediaTypes(JsonElement content, Func<JsonProperty, String> describeMediaType) =>
        String.Join(
            ", ",
            content
                .EnumerateObject()
                .Select(describeMediaType));

    private static String DescribeMediaType(JsonProperty mediaType) =>
        $"{mediaType.Name} {DescribeSchema(mediaType.Value.GetProperty("schema"))}";

    private static String DescribeSchema(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference) ? $"{reference}" : JsonSerializer.Serialize(schema);

    private static IEnumerable<String> DescribeRequestBody(DocumentOperation operation) =>
        operation.Element.TryGetProperty("requestBody", out var requestBody)
            ? [$"{operation.Name} {DescribeContent(requestBody)}"]
            : [];

    private static IReadOnlyList<String> ReadSchemaNames(JsonElement document) =>
        document
            .GetProperty("components")
            .GetProperty("schemas")
            .EnumerateObject()
            .Select(schema => schema.Name)
            .ToList();

    private static Boolean IsNamedAfterAMessageOrAnOptional(String schemaName) =>
        MessageSuffixes.Any(suffix => schemaName.EndsWith(suffix, StringComparison.Ordinal))
        || schemaName.StartsWith("OptionalOf", StringComparison.Ordinal);

    private static async Task<IReadOnlyList<ErrorAnswer>> ProduceEveryErrorAsync(
        TestModulesHost testModulesHost,
        HttpClient httpClient)
    {
        List<ErrorAnswer> answers = [];
        foreach (var kind in Enum.GetNames<ProducedErrorKind>())
        {
            var response = await httpClient.GetAsync($"/pings/errors/{kind}", CancellationToken);
            answers.Add(await ReadAnswerAsync(kind, ProduceErrorOperation, response));
        }

        var pingId = await testModulesHost.CreatePingAsync("thrown");
        var thrownResponse = await httpClient.PostAsync($"/pings/{pingId}/throw", content: null, CancellationToken);
        answers.Add(await ReadAnswerAsync("UnhandledException", ThrowAfterTouchOperation, thrownResponse));
        return answers;
    }

    private static async Task<ErrorAnswer> ReadAnswerAsync(
        String name,
        String operation,
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var contentType = $"{response.Content.Headers.ContentType?.MediaType}";
        return new ErrorAnswer(name, operation, (Int32)response.StatusCode, contentType, body);
    }

    private static String DescribeStatusCodeAndDetail(ErrorAnswer answer)
    {
        var code = ReadMember(answer.Body, "code");
        var detail = ReadMember(answer.Body, "detail");
        return $"{answer.Name} {answer.StatusCode} {code} {detail}";
    }

    private static IEnumerable<String> DescribeFieldErrors(ErrorAnswer answer) =>
        answer.Body.TryGetProperty("errors", out var errors)
            ? errors
                .EnumerateObject()
                .Select(field => $"{answer.Name} errors[{field.Name}]: {JoinMessages(field.Value)}")
            : [$"{answer.Name} (no errors)"];

    private static String JoinMessages(JsonElement messages) =>
        String.Join(
            ", ",
            messages
                .EnumerateArray()
                .Select(message => message.ToString()));

    private static String DescribeShape(ErrorAnswer answer) =>
        $"{DescribeRequest(answer)} {answer.ContentType} {JoinProblemMembers(answer.Body)}";

    private static String DescribeRequest(ErrorAnswer answer) =>
        $"{answer.Name} {answer.Operation} {answer.StatusCode}";

    private static String JoinProblemMembers(JsonElement body) =>
        String.Join(" ", ProblemMembers.Where(member => body.TryGetProperty(member, out _)));

    private static String DescribeDocumentedShape(JsonElement document, ErrorAnswer answer)
    {
        var operation = GetOperation(document, answer.Operation);
        return operation.Responses.TryGetProperty($"{answer.StatusCode}", out var response)
            ? $"{DescribeRequest(answer)} {DescribeDocumentedProblem(response)}"
            : $"{DescribeRequest(answer)} not described";
    }

    private static String DescribeDocumentedProblem(JsonElement response) =>
        response.TryGetProperty("content", out var content)
            ? JoinMediaTypes(content, DescribeDocumentedMembers)
            : "no content";

    private static String DescribeDocumentedMembers(JsonProperty mediaType)
    {
        var schema = ReadMember(mediaType.Value.GetProperty("schema"), "$ref");
        return $"{mediaType.Name} {MembersBySchema.GetValueOrDefault(schema, schema)}";
    }

    private static String ReadMember(JsonElement element, String name) =>
        element.TryGetProperty(name, out var value) ? value.ToString() : $"(no {name})";

    private static IReadOnlyList<RouteEndpoint> ReadRouteEndpoints(TestModulesHost testModulesHost) =>
        testModulesHost.Services
            .GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

    private static Boolean IsModuleEndpoint(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ITagsMetadata>() is not null;

    private static Boolean HasScenarioNameOfItsModule(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IRouteNameMetadata>() is { RouteName: { } routeName }
        && Regex.IsMatch(routeName, $@"^{Regex.Escape(GetModule(endpoint))}\.[A-Z][A-Za-z]+$");

    private static String GetModule(RouteEndpoint endpoint) =>
        endpoint.Metadata
            .GetRequiredMetadata<ITagsMetadata>().Tags
            .Single();

    private sealed record DocumentOperation(String Name, JsonElement Element)
    {
        public JsonElement Responses => Element.GetProperty("responses");
    }

    private sealed record ErrorAnswer(
        String Name,
        String Operation,
        Int32 StatusCode,
        String ContentType,
        JsonElement Body);
}
