using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Hosting;

internal sealed class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly PropertyInfo[] Keywords = typeof(OpenApiSchema)
        .GetProperties()
        .Where(IsKeyword)
        .ToArray();

    private readonly HashSet<Type> _describedTypes = [];

    private Int32 _valueSchemasInProgress;

    public static String? CreateSchemaReferenceId(JsonTypeInfo jsonTypeInfo) =>
        IsOptional(jsonTypeInfo.Type) ? null : OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);

    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (IsDescribedAgainInsideAValueSchema(context.JsonTypeInfo))
        {
            LeaveMembersToItsComponent(schema);
            return;
        }

        _describedTypes.Add(context.JsonTypeInfo.Type);
        LeaveOptionalsOutOfRequired(schema, context.JsonTypeInfo);
        if (IsOptional(context.JsonTypeInfo.Type))
        {
            await DescribeAsValueOrNullAsync(schema, context, cancellationToken);
        }
    }

    private static Boolean IsOptional(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);

    private Boolean IsDescribedAgainInsideAValueSchema(JsonTypeInfo jsonTypeInfo) =>
        _valueSchemasInProgress > 0
        && HasSchemaOfItsOwn(jsonTypeInfo)
        && _describedTypes.Contains(jsonTypeInfo.Type);

    private static Boolean HasSchemaOfItsOwn(JsonTypeInfo jsonTypeInfo) =>
        CreateSchemaReferenceId(jsonTypeInfo) is not null;

    private static void LeaveMembersToItsComponent(OpenApiSchema schema) => schema.Properties?.Clear();

    private static void LeaveOptionalsOutOfRequired(OpenApiSchema schema, JsonTypeInfo jsonTypeInfo) =>
        schema.Required?.ExceptWith(GetOptionalPropertyNames(jsonTypeInfo));

    private static IEnumerable<String> GetOptionalPropertyNames(JsonTypeInfo jsonTypeInfo) =>
        jsonTypeInfo.Properties
            .Where(property => IsOptional(property.PropertyType))
            .Select(property => property.Name);

    private async Task DescribeAsValueOrNullAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var valueTypeInfo = context.JsonTypeInfo.Options.GetTypeInfo(GetValueType(context.JsonTypeInfo.Type));
        var valueSchema = await BuildValueSchemaAsync(valueTypeInfo.Type, context, cancellationToken);
        if (IsPrimitive(valueTypeInfo))
        {
            DescribeAsNullable(schema, valueSchema);
            return;
        }

        schema.OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, valueSchema];
    }

    private static Type GetValueType(Type optionalType) => optionalType.GetGenericArguments()[0];

    private async Task<OpenApiSchema> BuildValueSchemaAsync(
        Type valueType,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        _valueSchemasInProgress++;
        try
        {
            return await context.GetOrCreateSchemaAsync(valueType, cancellationToken: cancellationToken);
        }
        finally
        {
            _valueSchemasInProgress--;
        }
    }

    private static Boolean IsPrimitive(JsonTypeInfo valueTypeInfo) =>
        valueTypeInfo.Kind == JsonTypeInfoKind.None && !valueTypeInfo.Type.IsEnum;

    private static void DescribeAsNullable(OpenApiSchema schema, OpenApiSchema valueSchema)
    {
        CopyKeywords(valueSchema, schema);
        schema.Type = valueSchema.Type | JsonSchemaType.Null;
    }

    private static void CopyKeywords(OpenApiSchema source, OpenApiSchema target)
    {
        foreach (var keyword in Keywords)
        {
            CopyKeywordWhenSet(keyword, source, target);
        }
    }

    private static void CopyKeywordWhenSet(PropertyInfo keyword, OpenApiSchema source, OpenApiSchema target)
    {
        if (keyword.GetValue(source) is { } value)
        {
            keyword.SetValue(target, value);
        }
    }

    private static Boolean IsKeyword(PropertyInfo property) =>
        property.CanWrite && property.Name != nameof(OpenApiSchema.Metadata);
}
