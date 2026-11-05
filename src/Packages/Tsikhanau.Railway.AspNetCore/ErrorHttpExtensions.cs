using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Tsikhanau.Railway;

public static class ErrorHttpExtensions
{
    public static IReadOnlyList<ErrorKind> ExpectedKinds { get; } =
    [
        ErrorKind.Validation,
        ErrorKind.Unauthorized,
        ErrorKind.Forbidden,
        ErrorKind.NotFound,
        ErrorKind.Conflict,
        ErrorKind.Failure
    ];

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

    public static Boolean IsExpected(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return ExpectedKinds.Contains(error.Kind)
            && (error is not AggregateError aggregateError || aggregateError.Errors.All(IsExpected));
    }

    public static Results<TSuccess, ValidationProblem, ProblemHttpResult> ToProblem<TSuccess>(this Error error)
        where TSuccess : IResult
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Kind is ErrorKind.Validation ? ToValidationProblem(error) : ToProblemHttpResult(error);
    }

    public static IResult ToHttpResult(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Kind is ErrorKind.Validation ? ToValidationProblem(error) : ToProblemHttpResult(error);
    }

    private static ValidationProblem ToValidationProblem(Error error) =>
        TypedResults.ValidationProblem(GroupMessagesByField(error), extensions: CodeOf(error));

    private static ProblemHttpResult ToProblemHttpResult(Error error) =>
        error.IsExpected()
            ? TypedResults.Problem(error.Message, statusCode: error.Kind.ToStatusCode(), extensions: CodeOf(error))
            : TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);

    private static Dictionary<String, Object?> CodeOf(Error error) => new() { [ProblemMembers.Code] = error.Code };

    private static Dictionary<String, String[]> GroupMessagesByField(Error error) =>
        CollectFieldErrors(error)
            .GroupBy(fieldError => fieldError.Field, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(fieldError => fieldError.Message)
                    .ToArray(),
                StringComparer.Ordinal);

    private static IEnumerable<FieldError> CollectFieldErrors(Error error) =>
        error switch
        {
            ValidationError validationError => validationError.FieldErrors,
            AggregateError aggregateError => aggregateError.Errors.SelectMany(CollectFieldErrors),
            _ when !error.IsExpected() => CollectWrappedFieldErrors(error),
            _ => [new FieldError(String.Empty, error.Message), .. CollectWrappedFieldErrors(error)]
        };

    private static IEnumerable<FieldError> CollectWrappedFieldErrors(Error error) =>
        error.Inner switch
        {
            ValidationError validationError => validationError.FieldErrors,
            AggregateError aggregateError => aggregateError.Errors.SelectMany(CollectFieldErrors),
            { } innerError => CollectWrappedFieldErrors(innerError),
            null => []
        };
}
