namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<TOutput> MatchAsync<T, TOutput>(
        this Task<Result<T>> resultTask,
        Func<T, TOutput> onSuccess,
        Func<Error, TOutput> onFailure)
        where T : notnull
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }

    public static async Task<TOutput> MatchAsync<T, TOutput>(
        this Task<Result<T>> resultTask,
        Func<T, Task<TOutput>> onSuccessAsync,
        Func<Error, Task<TOutput>> onFailureAsync)
        where T : notnull
    {
        var result = await resultTask;
        return result.IsSuccess
            ? await onSuccessAsync(result.Value)
            : await onFailureAsync(result.Error);
    }

    public static async Task<TOutput> MatchAsync<T, TOutput>(
        this Result<T> result,
        Func<T, Task<TOutput>> onSuccessAsync,
        Func<Error, Task<TOutput>> onFailureAsync)
        where T : notnull
    {
        return result.IsSuccess
            ? await onSuccessAsync(result.Value)
            : await onFailureAsync(result.Error);
    }
}
