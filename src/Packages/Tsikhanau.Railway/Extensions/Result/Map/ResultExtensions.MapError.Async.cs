namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<T>> MapErrorAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Error> errorMapper)
        where T : notnull
    {
        var result = await resultTask;
        return result.MapError(errorMapper);
    }

    public static async Task<Result<T>> MapErrorAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Task<Error>> errorMapperAsync)
        where T : notnull
    {
        var result = await resultTask;
        return result.IsSuccess
            ? result
            : Result<T>.Failure(await errorMapperAsync(result.Error));
    }

    public static async Task<Result<T>> MapErrorAsync<T>(
        this Result<T> result,
        Func<Error, Task<Error>> errorMapperAsync)
        where T : notnull
    {
        return result.IsSuccess
            ? result
            : Result<T>.Failure(await errorMapperAsync(result.Error));
    }
}
