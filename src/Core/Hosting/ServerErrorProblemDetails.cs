using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Tsikhanau.Monolith.Hosting;

internal static class ServerErrorProblemDetails
{
    public const String Code = "Server.Unexpected";

    private const String CodeKey = "code";

    public static void Customize(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            DescribeAsUnexpected(context.ProblemDetails);
        }
    }

    private static void DescribeAsUnexpected(ProblemDetails problemDetails)
    {
        problemDetails.Detail = null;
        problemDetails.Extensions[CodeKey] = Code;
    }
}
