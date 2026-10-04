namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<TResult>> MapAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, TResult> mapper)
        where T : notnull
        where TResult : notnull
    {
        var result = await resultTask;
        return result.Map(mapper);
    }

    public static async Task<Result<TResult>> MapAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, Task<TResult>> mapperAsync)
        where T : notnull
        where TResult : notnull
    {
        var result = await resultTask;
        return result.IsSuccess
            ? Result<TResult>.Success(await mapperAsync(result.Value))
            : Result<TResult>.Failure(result.Error);
    }

    public static async Task<Result<TResult>> MapAsync<T, TResult>(
        this Result<T> result,
        Func<T, Task<TResult>> mapperAsync)
        where T : notnull
        where TResult : notnull
    {
        return result.IsSuccess
            ? Result<TResult>.Success(await mapperAsync(result.Value))
            : Result<TResult>.Failure(result.Error);
    }
}
