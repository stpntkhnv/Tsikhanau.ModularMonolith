using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Tsikhanau.Monolith.Hosting;

internal static class BadRequestProblemDetails
{
    public const String Code = "Request.Invalid";

    private const String CodeKey = "code";
    private const String ErrorsKey = "errors";
    private const String ValidationTitle = "One or more validation errors occurred.";
    private const String RootPath = "$";
    private const String RootPathPrefix = "$.";
    private const String InvalidValueMessage = "The value is not valid.";
    private const String UnreadableRequestMessage = "The request could not be read.";
    private const String InvalidRouteOrQueryValueMessage = "A route or query value is not valid.";
    private const String InvalidBodyOrRouteOrQueryValueMessage = "The body or a route or query value is not valid.";

    public static Int32 SelectStatusCode(Exception exception) =>
        exception is BadHttpRequestException badHttpRequestException
            ? badHttpRequestException.StatusCode
            : StatusCodes.Status500InternalServerError;

    public static Boolean IsBadRequest(ExceptionHandlerSuppressDiagnosticsContext context) =>
        context.Exception is BadHttpRequestException;

    public static void Customize(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Status == StatusCodes.Status400BadRequest
            && context.Exception is BadHttpRequestException exception)
        {
            DescribeAsInvalidRequest(context.ProblemDetails, ToErrors(exception, context));
        }
    }

    private static void DescribeAsInvalidRequest(ProblemDetails problemDetails, Dictionary<String, String[]> errors)
    {
        problemDetails.Title = ValidationTitle;
        problemDetails.Extensions[ErrorsKey] = errors;
        problemDetails.Extensions[CodeKey] = Code;
    }

    private static Dictionary<String, String[]> ToErrors(
        BadHttpRequestException exception,
        ProblemDetailsContext context) =>
        exception.InnerException is JsonException jsonException
            ? new() { [ToField(jsonException.Path ?? RootPath)] = [InvalidValueMessage] }
            : new() { [RootPath] = [DescribeBindingFailure(context)] };

    private static String DescribeBindingFailure(ProblemDetailsContext context) =>
        TakesABody(context.AdditionalMetadata)
            ? DescribeBindingFailureOfABodyEndpoint(context.HttpContext)
            : InvalidRouteOrQueryValueMessage;

    private static String DescribeBindingFailureOfABodyEndpoint(HttpContext httpContext) =>
        CarriesABody(httpContext) ? InvalidBodyOrRouteOrQueryValueMessage : UnreadableRequestMessage;

    private static Boolean TakesABody(EndpointMetadataCollection? endpointMetadata) =>
        endpointMetadata?.GetMetadata<IAcceptsMetadata>() is not null;

    private static Boolean CarriesABody(HttpContext httpContext) =>
        httpContext.Features.Get<IHttpRequestBodyDetectionFeature>() is { CanHaveBody: true };

    private static String ToField(String path) =>
        path.StartsWith(RootPathPrefix, StringComparison.Ordinal) ? path[RootPathPrefix.Length..] : path;
}
