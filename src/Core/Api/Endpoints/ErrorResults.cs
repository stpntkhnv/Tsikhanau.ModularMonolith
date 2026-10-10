using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Endpoints;

internal static class ErrorResults
{
    private const String CodeKey = "code";

    public static Results<TSuccess, ValidationProblem, ProblemHttpResult> ToProblem<TSuccess>(Error error)
        where TSuccess : IResult =>
        error switch
        {
            _ when IsUnexpected(error) => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError),
            { Kind: ErrorKind.Validation } => TypedResults.ValidationProblem(
                GroupMessagesByField(error),
                extensions: CodeOf(error)),
            _ => TypedResults.Problem(
                error.Message,
                statusCode: error.Kind.ToStatusCode(),
                extensions: CodeOf(error))
        };

    private static Boolean IsUnexpected(Error error) =>
        !IsKnown(error.Kind) || (error is AggregateError aggregateError && aggregateError.Errors.Any(IsUnexpected));

    private static Boolean IsKnown(ErrorKind kind) =>
        kind is ErrorKind.Validation or ErrorKind.Unauthorized or ErrorKind.Forbidden or ErrorKind.NotFound
            or ErrorKind.Conflict or ErrorKind.Failure;

    private static Dictionary<String, Object?> CodeOf(Error error) => new() { [CodeKey] = error.Code };

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
