using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Endpoints;

public static class TypedResultExtensions
{
    public static Task<Results<Ok<T>, ValidationProblem, ProblemHttpResult>> ToOkAsync<T>(
        this Task<Result<T>> resultTask)
        where T : notnull =>
        resultTask.ToResultsAsync(TypedResults.Ok);

    public static Task<Results<CreatedAtRoute<T>, ValidationProblem, ProblemHttpResult>> ToCreatedAsync<T>(
        this Task<Result<T>> resultTask,
        String routeName,
        Func<T, Object> routeValues)
        where T : notnull =>
        resultTask.ToResultsAsync(value => TypedResults.CreatedAtRoute(value, routeName, routeValues(value)));

    public static Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ToNoContentAsync(
        this Task<Result<Unit>> resultTask) =>
        resultTask.ToResultsAsync(_ => TypedResults.NoContent());

    private static async Task<Results<TSuccess, ValidationProblem, ProblemHttpResult>> ToResultsAsync<T, TSuccess>(
        this Task<Result<T>> resultTask,
        Func<T, TSuccess> toSuccess)
        where T : notnull
        where TSuccess : IResult
    {
        var result = await resultTask;
        return result.IsSuccess
            ? toSuccess(result.Value)
            : result.Error.ToProblem<TSuccess>();
    }
}
