using Microsoft.AspNetCore.Http;

namespace Tsikhanau.Railway;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
        where T : notnull
    {
        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    public static IResult ToHttpResult(this Result<Unit> result)
    {
        return result.IsSuccess
            ? TypedResults.NoContent()
            : result.Error.ToHttpResult();
    }

    public static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : result.Error.ToHttpResult();
    }

    public static async Task<IResult> ToHttpResultAsync<T>(this Task<Result<T>> resultTask)
        where T : notnull
    {
        var result = await resultTask;
        return result.ToHttpResult();
    }

    public static async Task<IResult> ToHttpResultAsync(this Task<Result<Unit>> resultTask)
    {
        var result = await resultTask;
        return result.ToHttpResult();
    }

    public static async Task<IResult> ToHttpResultAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, IResult> onSuccess)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        var result = await resultTask;
        return result.ToHttpResult(onSuccess);
    }
}
