using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Tsikhanau.Monolith.Hosting;

internal sealed class AnyAcceptProblemDetailsWriter(
    IOptions<ProblemDetailsOptions> problemDetailsOptions,
    IOptions<JsonOptions> jsonOptions)
    : IProblemDetailsWriter
{
    private const String ProblemJson = "application/problem+json";
    private const String TraceIdKey = "traceId";

    private readonly JsonSerializerOptions _serializerOptions = jsonOptions.Value.SerializerOptions;

    private String TraceIdName => _serializerOptions.PropertyNamingPolicy?.ConvertName(TraceIdKey) ?? TraceIdKey;

    public Boolean CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ApplyDefaults(context.ProblemDetails, context.HttpContext.Response.StatusCode);
        context.ProblemDetails.Extensions[TraceIdName] = ReadTraceId(context.HttpContext);
        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);
        return new ValueTask(WriteAsJsonAsync(context));
    }

    private static void ApplyDefaults(ProblemDetails problemDetails, Int32 responseStatusCode)
    {
        var defaults = CreateDefaults(problemDetails.Status ?? responseStatusCode);
        problemDetails.Status = defaults.Status;
        problemDetails.Type ??= defaults.Type;
        problemDetails.Title ??= defaults.Title;
    }

    private static ProblemDetails CreateDefaults(Int32 statusCode) =>
        TypedResults.Problem(statusCode: statusCode).ProblemDetails;

    private static String ReadTraceId(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;

    private Task WriteAsJsonAsync(ProblemDetailsContext context) =>
        context.HttpContext.Response.WriteAsJsonAsync(
            context.ProblemDetails,
            _serializerOptions.GetTypeInfo(context.ProblemDetails.GetType()),
            ProblemJson);
}
