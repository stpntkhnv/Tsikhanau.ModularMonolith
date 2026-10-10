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
    private const String ComponentSchemaPrefix = "#/components/schemas/";
    private const String ProblemDetailsName = "ProblemDetails";
    private const String ValidationProblemDetailsName = "HttpValidationProblemDetails";
    private const String ProblemDetailsSchema = $"{ComponentSchemaPrefix}{ProblemDetailsName}";
    private const String ValidationProblemDetailsSchema = $"{ComponentSchemaPrefix}{ValidationProblemDetailsName}";
    private const String ProblemJson = "application/problem+json";

    private static readonly String[] ModulePathPrefixes = ["/pings", "/pongs"];

    private static readonly String[] MessageSuffixes = ["Command", "Query"];

    private static readonly String[] OperationMethods = ["get", "put", "post", "delete", "patch"];

    private static readonly String[] ProblemSchemaNames = [ProblemDetailsName, ValidationProblemDetailsName];

    private static readonly String[] ProblemExtensionMembers = ["code", "traceId"];

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
        var differences = answers.SelectMany(answer => FindDifferencesFromTheDocument(document, answer));

        differences.ShouldBeEmpty();
    }

    [Fact]
    public async Task Problem_schemas_describe_the_code_and_the_trace_id_as_strings_that_are_not_required()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var extensionMembers = ProblemSchemaNames.SelectMany(name => DescribeExtensionMembers(document, name));

        extensionMembers.ShouldBe(
        [
            """ProblemDetails code: {"type":"string"}""",
            """ProblemDetails traceId: {"type":"string"}""",
            "ProblemDetails required: (none)",
            """HttpValidationProblemDetails code: {"type":"string"}""",
            """HttpValidationProblemDetails traceId: {"type":"string"}""",
            "HttpValidationProblemDetails required: (none)"
        ]);
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

    private static JsonElement ReadSchemas(JsonElement document) =>
        document
            .GetProperty("components")
            .GetProperty("schemas");

    private static IReadOnlyList<String> ReadSchemaNames(JsonElement document) =>
        ReadSchemas(document)
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

    private static String DescribeRequest(ErrorAnswer answer) =>
        $"{answer.Name} {answer.Operation} {answer.StatusCode}";

    private static IEnumerable<String> FindDifferencesFromTheDocument(JsonElement document, ErrorAnswer answer)
    {
        var responses = GetOperation(document, answer.Operation).Responses;
        if (!responses.TryGetProperty($"{answer.StatusCode}", out var response))
        {
            return [$"{DescribeRequest(answer)}: not a response of the operation"];
        }

        if (!TryReadMediaTypeSchema(response, answer.ContentType, out var schema))
        {
            return [$"{DescribeRequest(answer)}: {answer.ContentType} is not a content type of the response"];
        }

        var reference = ReadMember(schema, "$ref");
        if (!TryReadComponent(document, reference, out var component))
        {
            return [$"{DescribeRequest(answer)}: {reference} is not a component of the document"];
        }

        return
        [
            .. FindStatusDifferences(answer),
            .. FindValueDifferences($"{DescribeRequest(answer)} {reference}", answer.Body, component)
        ];
    }

    private static Boolean TryReadMediaTypeSchema(JsonElement response, String contentType, out JsonElement schema)
    {
        schema = default;
        return response.TryGetProperty("content", out var content)
            && content.TryGetProperty(contentType, out var mediaType)
            && mediaType.TryGetProperty("schema", out schema);
    }

    private static Boolean TryReadComponent(JsonElement document, String reference, out JsonElement component)
    {
        component = default;
        return reference.StartsWith(ComponentSchemaPrefix, StringComparison.Ordinal)
            && ReadSchemas(document)
                .TryGetProperty(reference[ComponentSchemaPrefix.Length..], out component);
    }

    private static IEnumerable<String> FindStatusDifferences(ErrorAnswer answer)
    {
        var status = ReadMember(answer.Body, "status");
        return status == $"{answer.StatusCode}"
            ? []
            : [$"{DescribeRequest(answer)}: the body says status {status}"];
    }

    private static IEnumerable<String> FindValueDifferences(String path, JsonElement value, JsonElement schema) =>
        AllowsTypeOf(schema, value)
            ? FindDifferencesInside(path, value, schema)
            : [$"{path}: {value.GetRawText()} where the document declares {JsonSerializer.Serialize(schema)}"];

    private static Boolean AllowsTypeOf(JsonElement schema, JsonElement value) =>
        ReadDeclaredTypes(schema)
            .Intersect(ReadJsonTypes(value))
            .Any();

    private static IEnumerable<String> ReadDeclaredTypes(JsonElement schema) =>
        schema.TryGetProperty("type", out var type) ? ReadOneOrMany(type) : [];

    private static IEnumerable<String> ReadOneOrMany(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array
            ? element
                .EnumerateArray()
                .Select(item => $"{item}")
            : [$"{element}"];

    private static IEnumerable<String> ReadJsonTypes(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => ["string"],
            JsonValueKind.Number => value.TryGetInt64(out _) ? ["integer", "number"] : ["number"],
            JsonValueKind.True or JsonValueKind.False => ["boolean"],
            JsonValueKind.Object => ["object"],
            JsonValueKind.Array => ["array"],
            _ => ["null"]
        };

    private static IEnumerable<String> FindDifferencesInside(String path, JsonElement value, JsonElement schema) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => value
                .EnumerateObject()
                .SelectMany(member => FindMemberDifferences(path, member, schema)),
            JsonValueKind.Array => FindItemDifferences(path, value, schema),
            _ => []
        };

    private static IEnumerable<String> FindMemberDifferences(String path, JsonProperty member, JsonElement schema) =>
        TryReadMemberSchema(schema, member.Name, out var memberSchema)
            ? FindValueDifferences($"{path} {member.Name}", member.Value, memberSchema)
            : [$"{path} {member.Name}: not declared"];

    private static Boolean TryReadMemberSchema(JsonElement schema, String name, out JsonElement memberSchema) =>
        TryReadDeclaredMemberSchema(schema, name, out memberSchema)
        || TryReadAdditionalMemberSchema(schema, out memberSchema);

    private static Boolean TryReadDeclaredMemberSchema(JsonElement schema, String name, out JsonElement memberSchema)
    {
        memberSchema = default;
        return schema.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty(name, out memberSchema);
    }

    private static Boolean TryReadAdditionalMemberSchema(JsonElement schema, out JsonElement memberSchema) =>
        schema.TryGetProperty("additionalProperties", out memberSchema)
        && memberSchema.ValueKind == JsonValueKind.Object;

    private static IEnumerable<String> FindItemDifferences(String path, JsonElement value, JsonElement schema) =>
        schema.TryGetProperty("items", out var itemSchema)
            ? value
                .EnumerateArray()
                .SelectMany((item, index) => FindValueDifferences($"{path}[{index}]", item, itemSchema))
            : [$"{path}: items not declared"];

    private static IEnumerable<String> DescribeExtensionMembers(JsonElement document, String schemaName)
    {
        var schema = ReadSchemas(document)
            .GetProperty(schemaName);

        var members = ProblemExtensionMembers.Select(
            member => $"{schemaName} {member}: {DescribeMemberSchema(schema, member)}");

        return [.. members, $"{schemaName} required: {DescribeRequired(schema)}"];
    }

    private static String DescribeMemberSchema(JsonElement schema, String member) =>
        TryReadDeclaredMemberSchema(schema, member, out var memberSchema)
            ? JsonSerializer.Serialize(memberSchema)
            : "(not declared)";

    private static String DescribeRequired(JsonElement schema) =>
        schema.TryGetProperty("required", out var required)
            ? String.Join(", ", ReadOneOrMany(required))
            : "(none)";

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
