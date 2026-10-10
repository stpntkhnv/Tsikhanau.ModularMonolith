using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Hosting;

internal sealed class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    public static String? CreateSchemaReferenceId(JsonTypeInfo jsonTypeInfo) =>
        IsOptional(jsonTypeInfo.Type) ? null : OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);

    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        LeaveOptionalsOutOfRequired(schema, context.JsonTypeInfo);
        if (IsOptional(context.JsonTypeInfo.Type))
        {
            await DescribeAsValueOrNullAsync(schema, context, cancellationToken);
        }
    }

    private static Boolean IsOptional(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);

    private static void LeaveOptionalsOutOfRequired(OpenApiSchema schema, JsonTypeInfo jsonTypeInfo) =>
        schema.Required?.ExceptWith(GetOptionalPropertyNames(jsonTypeInfo));

    private static IEnumerable<String> GetOptionalPropertyNames(JsonTypeInfo jsonTypeInfo) =>
        jsonTypeInfo.Properties
            .Where(property => IsOptional(property.PropertyType))
            .Select(property => property.Name);

    private static async Task DescribeAsValueOrNullAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var valueTypeInfo = context.JsonTypeInfo.Options.GetTypeInfo(GetValueType(context.JsonTypeInfo.Type));
        var valueSchema = await context.GetOrCreateSchemaAsync(
            valueTypeInfo.Type,
            cancellationToken: cancellationToken);

        if (IsPrimitive(valueTypeInfo))
        {
            DescribeAsNullable(schema, valueSchema);
            return;
        }

        schema.OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, valueSchema];
    }

    private static Type GetValueType(Type optionalType) => optionalType.GetGenericArguments()[0];

    private static Boolean IsPrimitive(JsonTypeInfo valueTypeInfo) =>
        valueTypeInfo.Kind == JsonTypeInfoKind.None && !valueTypeInfo.Type.IsEnum;

    private static void DescribeAsNullable(OpenApiSchema schema, OpenApiSchema valueSchema)
    {
        schema.Type = valueSchema.Type | JsonSchemaType.Null;
        schema.Format = valueSchema.Format;
        schema.Pattern = valueSchema.Pattern;
    }
}
