using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Tsikhanau.Monolith.Hosting;

internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    private const String CodeKey = "code";
    private const String TraceIdKey = "traceId";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (IsProblemDetails(context.JsonTypeInfo.Type))
        {
            DescribeExtensions(schema);
        }

        return Task.CompletedTask;
    }

    private static Boolean IsProblemDetails(Type type) => type.IsAssignableTo(typeof(ProblemDetails));

    private static void DescribeExtensions(OpenApiSchema schema)
    {
        var properties = schema.Properties ??= new Dictionary<String, IOpenApiSchema>();
        properties[CodeKey] = new OpenApiSchema { Type = JsonSchemaType.String };
        properties[TraceIdKey] = new OpenApiSchema { Type = JsonSchemaType.String };
    }
}
