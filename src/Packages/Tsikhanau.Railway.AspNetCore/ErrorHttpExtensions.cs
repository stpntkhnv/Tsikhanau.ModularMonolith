using Microsoft.AspNetCore.Http;

namespace Tsikhanau.Railway;

public static class ErrorHttpExtensions
{
    public static Int32 ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Failure => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };

    public static IResult ToHttpResult(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (IsUnexpected(error))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        var extensions = new Dictionary<String, Object?> { ["code"] = error.Code };

        if (error is ValidationError validationError)
        {
            return TypedResults.ValidationProblem(
                ToValidationErrors(validationError),
                extensions: extensions);
        }

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: error.Kind.ToStatusCode(),
            extensions: extensions);
    }

    private static Boolean IsUnexpected(Error error)
    {
        if (error.Kind is not (ErrorKind.Failure or ErrorKind.Validation or ErrorKind.NotFound
            or ErrorKind.Conflict or ErrorKind.Unauthorized or ErrorKind.Forbidden))
        {
            return true;
        }

        if (error is AggregateError aggregateError)
        {
            foreach (var inner in aggregateError.Errors)
            {
                if (IsUnexpected(inner))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<String, String[]> ToValidationErrors(ValidationError validationError)
    {
        var errors = new Dictionary<String, List<String>>(StringComparer.Ordinal);
        foreach (var fieldError in validationError.FieldErrors)
        {
            if (!errors.TryGetValue(fieldError.Field, out var messages))
            {
                messages = [];
                errors.Add(fieldError.Field, messages);
            }

            messages.Add(fieldError.Message);
        }

        var result = new Dictionary<String, String[]>(errors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in errors)
        {
            result.Add(field, messages.ToArray());
        }

        return result;
    }
}
