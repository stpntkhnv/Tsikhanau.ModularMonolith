using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Hosting;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Hosting;

public sealed class OptionalSchemaTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Optional_values_are_described_as_their_value_or_null_and_are_not_required()
    {
        await using var app = await StartProbeAppAsync();
        using var httpClient = app.GetTestClient();

        var document = await httpClient.GetFromJsonAsync<JsonElement>("/openapi/v1.json", CancellationToken);
        var probe = DescribeSchema(document, nameof(OptionalProbe));

        probe.ShouldBe(
        [
            "required: label",
            """label: {"type":"string"}""",
            """count: {"pattern":"^-?(?:0|[1-9]\\d*)$","type":["null","integer","string"],"format":"int32"}""",
            """item: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeItem"}]}""",
            """kind: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeKind"}]}"""
        ]);
    }

    [Fact]
    public async Task Optional_gets_no_schema_of_its_own_and_its_value_type_does()
    {
        await using var app = await StartProbeAppAsync();
        using var httpClient = app.GetTestClient();

        var document = await httpClient.GetFromJsonAsync<JsonElement>("/openapi/v1.json", CancellationToken);
        var schemaNames = document
            .GetProperty("components")
            .GetProperty("schemas")
            .EnumerateObject()
            .Select(schema => schema.Name);

        schemaNames.ShouldBe([nameof(OptionalProbe), nameof(ProbeItem), nameof(ProbeKind)], ignoreOrder: true);
    }

    private static async Task<WebApplication> StartProbeAppAsync()
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Development });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();

        var app = builder.Build();
        app.UseServiceDefaults();
        app.MapPost("/probe", (OptionalProbe probe) => TypedResults.Ok(probe));
        await app.StartAsync(CancellationToken);
        return app;
    }

    private static IEnumerable<String> DescribeSchema(JsonElement document, String name)
    {
        var schema = document
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(name);

        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(property => $"{property}");

        var properties = schema
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => $"{property.Name}: {JsonSerializer.Serialize(property.Value)}");

        return [$"required: {String.Join(", ", required)}", .. properties];
    }

    private sealed record OptionalProbe(
        String Label,
        Optional<Int32> Count,
        Optional<ProbeItem> Item,
        Optional<ProbeKind> Kind);

    private sealed record ProbeItem(String Name);

    private enum ProbeKind
    {
        Small,
        Large
    }
}
