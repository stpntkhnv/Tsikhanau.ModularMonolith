using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Tsikhanau.Monolith.Api.Endpoints;

namespace Tsikhanau.Monolith.Hosting.OpenApi;

internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
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
        properties[ProblemMembers.Code] = new OpenApiSchema { Type = JsonSchemaType.String };
        properties[ProblemMembers.TraceId] = new OpenApiSchema { Type = JsonSchemaType.String };
    }
}
