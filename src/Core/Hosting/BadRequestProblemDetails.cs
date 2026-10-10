using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
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
            DescribeAsInvalidRequest(context.ProblemDetails, ToErrors(exception));
        }
    }

    private static void DescribeAsInvalidRequest(ProblemDetails problemDetails, Dictionary<String, String[]> errors)
    {
        problemDetails.Title = ValidationTitle;
        problemDetails.Extensions[ErrorsKey] = errors;
        problemDetails.Extensions[CodeKey] = Code;
    }

    private static Dictionary<String, String[]> ToErrors(BadHttpRequestException exception) =>
        exception.InnerException is JsonException jsonException
            ? new() { [ToField(jsonException.Path ?? RootPath)] = [InvalidValueMessage] }
            : new() { [RootPath] = [UnreadableRequestMessage] };

    private static String ToField(String path) =>
        path.StartsWith(RootPathPrefix, StringComparison.Ordinal) ? path[RootPathPrefix.Length..] : path;
}
