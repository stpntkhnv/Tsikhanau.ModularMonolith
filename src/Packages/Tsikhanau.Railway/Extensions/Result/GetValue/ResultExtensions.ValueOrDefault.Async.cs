namespace Tsikhanau.Railway;

public static partial class ResultExtensions
{
    public static async Task<T?> GetValueOrDefaultAsync<T>(this Task<Result<T>> resultTask)
        where T : notnull
    {
        var result = await resultTask;
        return result.GetValueOrDefault();
    }

    public static async Task<T> GetValueOrDefaultAsync<T>(
        this Task<Result<T>> resultTask,
        T defaultValue)
        where T : notnull
    {
        var result = await resultTask;
        return result.GetValueOrDefault(defaultValue);
    }

    public static async Task<T> GetValueOrDefaultAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T> defaultValueFactory)
        where T : notnull
    {
        var result = await resultTask;
        return result.GetValueOrDefault(defaultValueFactory);
    }

    public static async Task<T> GetValueOrDefaultAsync<T>(
        this Task<Result<T>> resultTask,
        Func<Task<T>> defaultValueFactoryAsync)
        where T : notnull
    {
        var result = await resultTask;
        return result.IsSuccess ? result.Value : await defaultValueFactoryAsync();
    }
}
