using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Tsikhanau.Monolith.Hosting;

internal static class ServerErrorProblemDetails
{
    public const String Code = "Server.Unexpected";

    private const String CodeKey = "code";

    private static readonly ProblemDetails Defaults =
        TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError).ProblemDetails;

    public static void Customize(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            DescribeAsUnexpected(context.ProblemDetails, ResolveSerializerOptions(context.HttpContext).TraceIdKey);
        }
    }

    private static void DescribeAsUnexpected(ProblemDetails problemDetails, String traceIdKey)
    {
        problemDetails.Title = Defaults.Title;
        problemDetails.Type = Defaults.Type;
        problemDetails.Detail = null;
        problemDetails.Instance = null;
        KeepOnlyTheTraceId(problemDetails.Extensions, traceIdKey);
        problemDetails.Extensions[CodeKey] = Code;
    }

    private static void KeepOnlyTheTraceId(IDictionary<String, Object?> extensions, String traceIdKey) =>
        extensions.Keys
            .Where(key => key != traceIdKey)
            .ToList()
            .ForEach(key => extensions.Remove(key));

    private static JsonSerializerOptions ResolveSerializerOptions(HttpContext httpContext) =>
        httpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value
            .SerializerOptions;
}
