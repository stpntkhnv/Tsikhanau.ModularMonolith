namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<TResult>> BindAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, Result<TResult>> binder)
        where T : notnull
        where TResult : notnull
    {
        var result = await resultTask;
        return result.Bind(binder);
    }

    public static async Task<Result<TResult>> BindAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Result<TResult>>> binderAsync)
        where T : notnull
        where TResult : notnull
    {
        var result = await resultTask;
        return result.IsSuccess
            ? await binderAsync(result.Value)
            : Result<TResult>.Failure(result.Error);
    }

    public static async Task<Result<TResult>> BindAsync<T, TResult>(
        this Result<T> result,
        Func<T, Task<Result<TResult>>> binderAsync)
        where T : notnull
        where TResult : notnull
    {
        return result.IsSuccess
            ? await binderAsync(result.Value)
            : Result<TResult>.Failure(result.Error);
    }
}
