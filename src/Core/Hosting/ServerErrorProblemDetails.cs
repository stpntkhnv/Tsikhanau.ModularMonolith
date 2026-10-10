using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Tsikhanau.Monolith.Hosting;

internal static class ServerErrorProblemDetails
{
    public const String Code = "Server.Unexpected";

    private const String CodeKey = "code";
    private const String TraceIdKey = "traceId";

    private static readonly ProblemDetails Defaults =
        TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError).ProblemDetails;

    public static void Customize(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            DescribeAsUnexpected(context.ProblemDetails);
        }
    }

    private static void DescribeAsUnexpected(ProblemDetails problemDetails)
    {
        problemDetails.Title = Defaults.Title;
        problemDetails.Type = Defaults.Type;
        problemDetails.Detail = null;
        problemDetails.Instance = null;
        KeepOnlyTheTraceId(problemDetails.Extensions);
        problemDetails.Extensions[CodeKey] = Code;
    }

    private static void KeepOnlyTheTraceId(IDictionary<String, Object?> extensions) =>
        extensions.Keys
            .Where(key => key != TraceIdKey)
            .ToList()
            .ForEach(key => extensions.Remove(key));
}
