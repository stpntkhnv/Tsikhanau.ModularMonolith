namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Boolean> predicate,
        Error error)
        where T : notnull
    {
        var result = await resultTask;
        return result.Ensure(predicate, error);
    }

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Boolean> predicate,
        Func<T, Error> errorFactory)
        where T : notnull
    {
        var result = await resultTask;
        return result.Ensure(predicate, errorFactory);
    }

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Boolean>> predicateAsync,
        Error error)
        where T : notnull
    {
        var result = await resultTask;
        if (result.IsFailure || await predicateAsync(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(error);
    }

    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Boolean>> predicateAsync,
        Func<T, Error> errorFactory)
        where T : notnull
    {
        var result = await resultTask;
        if (result.IsFailure || await predicateAsync(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(errorFactory(result.Value));
    }

    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<Boolean>> predicateAsync,
        Error error)
        where T : notnull
    {
        if (result.IsFailure || await predicateAsync(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(error);
    }

    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<Boolean>> predicateAsync,
        Func<T, Error> errorFactory)
        where T : notnull
    {
        if (result.IsFailure || await predicateAsync(result.Value))
        {
            return result;
        }

        return Result<T>.Failure(errorFactory(result.Value));
    }
}
