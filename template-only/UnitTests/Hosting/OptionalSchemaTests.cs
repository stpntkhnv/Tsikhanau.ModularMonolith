using System.ComponentModel.DataAnnotations;
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
    private const String TreeChildren =
        """children: {"oneOf":[{"type":"null"},{"type":"array","items":{"$ref":"#/components/schemas/ProbeTree"}}]}""";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Optional_values_are_described_as_their_value_or_null_and_are_not_required()
    {
        await using var app = await StartProbeAppAsync((OptionalProbe probe) => TypedResults.Ok(probe));

        var document = await ReadDocumentAsync(app);
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
        await using var app = await StartProbeAppAsync((OptionalProbe probe) => TypedResults.Ok(probe));

        var document = await ReadDocumentAsync(app);
        var schemaNames = ReadSchemaNames(document);

        schemaNames.ShouldBe([nameof(OptionalProbe), nameof(ProbeItem), nameof(ProbeKind)], ignoreOrder: true);
    }

    [Fact]
    public async Task Optional_primitive_keeps_every_keyword_of_its_value_schema_and_of_its_property()
    {
        await using var app = await StartProbeAppAsync((ConstrainedProbe probe) => TypedResults.Ok(probe));

        var document = await ReadDocumentAsync(app);
        var probe = DescribeSchema(document, nameof(ConstrainedProbe));

        probe.ShouldBe(
        [
            "required: ",
            """letter: {"maxLength":1,"minLength":1,"type":["null","string"],"format":"char"}""",
            """code: {"maxLength":20,"type":["null","string"]}"""
        ]);
    }

    [Fact]
    public async Task Optional_value_type_used_twice_keeps_its_whole_schema()
    {
        await using var app = await StartProbeAppAsync((ProbePair pair) => TypedResults.Ok(pair));

        var document = await ReadDocumentAsync(app);
        IEnumerable<String> schemas =
        [
            .. DescribeSchema(document, nameof(ProbePair)),
            .. DescribeSchema(document, nameof(ProbeItem))
        ];

        schemas.ShouldBe(
        [
            "required: ",
            """first: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeItem"}]}""",
            """second: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeItem"}]}""",
            "required: name",
            """name: {"type":"string"}"""
        ]);
    }

    [Fact]
    public async Task Self_recursive_optional_value_is_described_by_a_reference_to_its_type()
    {
        await using var app = await StartProbeAppAsync((ProbeNode node) => TypedResults.Ok(node));

        var document = await ReadDocumentAsync(app);
        var node = DescribeSchema(document, nameof(ProbeNode));
        var schemaNames = ReadSchemaNames(document);

        node.ShouldBe(
        [
            "required: name",
            """name: {"type":"string"}""",
            """next: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeNode"}]}"""
        ]);

        schemaNames.ShouldBe([nameof(ProbeNode)]);
    }

    [Fact]
    public async Task Mutually_recursive_optional_values_are_described_by_references_to_each_other()
    {
        await using var app = await StartProbeAppAsync((ProbeParent parent) => TypedResults.Ok(parent));

        var document = await ReadDocumentAsync(app);
        IEnumerable<String> schemas =
        [
            .. DescribeSchema(document, nameof(ProbeParent)),
            .. DescribeSchema(document, nameof(ProbeChild))
        ];

        var schemaNames = ReadSchemaNames(document);

        schemas.ShouldBe(
        [
            "required: name",
            """name: {"type":"string"}""",
            """child: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeChild"}]}""",
            "required: name",
            """name: {"type":"string"}""",
            """parent: {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/ProbeParent"}]}"""
        ]);

        schemaNames.ShouldBe([nameof(ProbeChild), nameof(ProbeParent)], ignoreOrder: true);
    }

    [Fact]
    public async Task Optional_collection_of_its_own_type_is_described_by_a_reference_to_the_item_type()
    {
        await using var app = await StartProbeAppAsync((ProbeTree tree) => TypedResults.Ok(tree));

        var document = await ReadDocumentAsync(app);
        var tree = DescribeSchema(document, nameof(ProbeTree));
        var schemaNames = ReadSchemaNames(document);

        tree.ShouldBe(
        [
            "required: name",
            """name: {"type":"string"}""",
            TreeChildren
        ]);

        schemaNames.ShouldBe([nameof(ProbeTree)]);
    }

    private static async Task<WebApplication> StartProbeAppAsync(Delegate probeHandler)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Development });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddHttpDefaults();

        var app = builder.Build();
        app.UseServiceDefaults();
        app.MapPost("/probe", probeHandler);
        await app.StartAsync(CancellationToken);
        return app;
    }

    private static async Task<JsonElement> ReadDocumentAsync(WebApplication app)
    {
        using var httpClient = app.GetTestClient();
        return await httpClient.GetFromJsonAsync<JsonElement>("/openapi/v1.json", CancellationToken);
    }

    private static JsonElement ReadSchemas(JsonElement document) =>
        document
            .GetProperty("components")
            .GetProperty("schemas");

    private static IEnumerable<String> ReadSchemaNames(JsonElement document) =>
        ReadSchemas(document)
            .EnumerateObject()
            .Select(schema => schema.Name);

    private static IEnumerable<String> DescribeSchema(JsonElement document, String name)
    {
        var schema = ReadSchemas(document)
            .GetProperty(name);

        var required = schema.TryGetProperty("required", out var requiredProperties)
            ? requiredProperties
                .EnumerateArray()
                .Select(property => $"{property}")
            : [];

        var properties = schema.TryGetProperty("properties", out var declaredProperties)
            ? declaredProperties
                .EnumerateObject()
                .Select(property => $"{property.Name}: {JsonSerializer.Serialize(property.Value)}")
            : [];

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

    private sealed record ConstrainedProbe(Optional<Char> Letter, [property: MaxLength(20)] Optional<String> Code);

    private sealed record ProbePair(Optional<ProbeItem> First, Optional<ProbeItem> Second);

    private sealed record ProbeNode(String Name, Optional<ProbeNode> Next);

    private sealed record ProbeParent(String Name, Optional<ProbeChild> Child);

    private sealed record ProbeChild(String Name, Optional<ProbeParent> Parent);

    private sealed record ProbeTree(String Name, Optional<IReadOnlyList<ProbeTree>> Children);
}
