namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<T>> TapErrorAsync<T>(
        this Task<Result<T>> resultTask,
        Action<Error> action)
        where T : notnull
    {
        var result = await resultTask;
        return result.TapError(action);
    }

    public static async Task<Result<T>> TapErrorAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Error, Task> actionAsync)
        where T : notnull
    {
        var result = await resultTask;
        if (result.IsFailure)
        {
            await actionAsync(result.Error);
        }

        return result;
    }

    public static async Task<Result<T>> TapErrorAsync<T>(
        this Result<T> result,
        Func<Error, Task> actionAsync)
        where T : notnull
    {
        if (result.IsFailure)
        {
            await actionAsync(result.Error);
        }

        return result;
    }
}
