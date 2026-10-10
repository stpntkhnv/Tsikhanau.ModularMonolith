using System.Net.Http.Json;
using System.Text.Json;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.Api;

public sealed class OptionalJsonTests(PostgresFixture postgresFixture)
{
    private const String NullableString = """{"type":["null","string"]}""";

    private static readonly Guid PingId = Guid.Parse("0199c2a4-5e00-7000-8000-000000000001");

    private static readonly JsonElement MissingSchema = JsonSerializer.SerializeToElement(new { });

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Http_json_options_write_none_as_null_and_read_a_missing_property_as_none()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        var jsonSerializerOptions = testModulesHost.HttpJsonSerializerOptions;

        var writtenPing = JsonSerializer.Serialize(
            new PingDto(PingId, "label", Optional<String>.None(), 0, 1),
            jsonSerializerOptions);

        var readRequest = JsonSerializer.Deserialize<CreatePingRequest>("""{"label":"label"}""", jsonSerializerOptions);

        writtenPing.ShouldBe($$"""{"id":"{{PingId}}","label":"label","nickname":null,"touches":0,"version":1}""");
        readRequest.ShouldBe(new CreatePingRequest("label", Optional<String>.None()));
    }

    [Fact]
    public async Task Request_record_describes_its_optional_property_as_nullable_and_not_required()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var createPingRequest = ReadSchema(document, "CreatePingRequest");
        var required = ReadRequired(createPingRequest);
        var nickname = ReadProperty(createPingRequest, "nickname");

        required.ShouldBe(["label"]);
        nickname.ShouldBe(NullableString);
    }

    [Fact]
    public async Task Dto_describes_its_optional_property_as_nullable_and_not_required()
    {
        await using var testModulesHost = await TestModulesHost.StartAsync(postgresFixture);
        using var httpClient = testModulesHost.CreateClient();

        var document = await ReadDocumentAsync(httpClient);
        var pingDto = ReadSchema(document, "PingDto");
        var required = ReadRequired(pingDto);
        var nickname = ReadProperty(pingDto, "nickname");

        required.ShouldBe(["id", "label", "touches", "version"], ignoreOrder: true);
        nickname.ShouldBe(NullableString);
    }

    private static Task<JsonElement> ReadDocumentAsync(HttpClient httpClient) =>
        httpClient.GetFromJsonAsync<JsonElement>("/openapi/v1.json", CancellationToken);

    private static JsonElement ReadSchema(JsonElement document, String name) =>
        document
            .GetProperty("components")
            .GetProperty("schemas")
            .TryGetProperty(name, out var schema)
            ? schema
            : MissingSchema;

    private static IReadOnlyList<String> ReadRequired(JsonElement schema) =>
        schema.TryGetProperty("required", out var required)
            ? [.. required
                .EnumerateArray()
                .Select(name => $"{name}")]
            : [];

    private static String ReadProperty(JsonElement schema, String name) =>
        schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(name, out var property)
            ? JsonSerializer.Serialize(property)
            : $"(no {name})";
}
