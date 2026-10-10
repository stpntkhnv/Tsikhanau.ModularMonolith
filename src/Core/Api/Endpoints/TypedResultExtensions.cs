using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Api.Endpoints;

public static class TypedResultExtensions
{
    extension<T>(Task<Result<T>> resultTask)
        where T : notnull
    {
        public Task<Results<Ok<T>, ValidationProblem, ProblemHttpResult>> ToOkAsync() =>
            resultTask.ToResultsAsync(TypedResults.Ok);

        public Task<Results<CreatedAtRoute<T>, ValidationProblem, ProblemHttpResult>> ToCreatedAsync(
            String routeName,
            Func<T, Object> routeValues) =>
            resultTask.ToResultsAsync(value => TypedResults.CreatedAtRoute(value, routeName, routeValues(value)));

        private async Task<Results<TSuccess, ValidationProblem, ProblemHttpResult>> ToResultsAsync<TSuccess>(
            Func<T, TSuccess> toSuccess)
            where TSuccess : IResult
        {
            var result = await resultTask;
            return result.IsSuccess
                ? toSuccess(result.Value)
                : ErrorResults.ToProblem<TSuccess>(result.Error);
        }
    }

    extension(Task<Result<Unit>> resultTask)
    {
        public Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ToNoContentAsync() =>
            resultTask.ToResultsAsync(_ => TypedResults.NoContent());
    }
}
