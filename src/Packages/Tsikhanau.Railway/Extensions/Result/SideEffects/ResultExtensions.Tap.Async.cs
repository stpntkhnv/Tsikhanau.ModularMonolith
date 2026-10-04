namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> resultTask,
        Action<T> action)
        where T : notnull
    {
        var result = await resultTask;
        return result.Tap(action);
    }

    public static async Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task> actionAsync)
        where T : notnull
    {
        var result = await resultTask;
        if (result.IsSuccess)
        {
            await actionAsync(result.Value);
        }

        return result;
    }

    public static async Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, Task> actionAsync)
        where T : notnull
    {
        if (result.IsSuccess)
        {
            await actionAsync(result.Value);
        }

        return result;
    }
}
