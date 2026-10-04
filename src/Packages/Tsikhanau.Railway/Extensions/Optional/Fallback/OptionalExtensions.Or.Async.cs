namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    public static async Task<Optional<T>> OrAsync<T>(
        this Task<Optional<T>> optionalTask,
        Optional<T> fallback)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.Or(fallback);
    }

    public static async Task<Optional<T>> OrElseAsync<T>(
        this Task<Optional<T>> optionalTask,
        Func<Optional<T>> fallbackFactory)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.OrElse(fallbackFactory);
    }

    public static async Task<Optional<T>> OrElseAsync<T>(
        this Optional<T> optional,
        Func<Task<Optional<T>>> fallbackFactoryAsync)
        where T : notnull
    {
        return optional.HasValue ? optional : await fallbackFactoryAsync();
    }

    public static async Task<Optional<T>> OrElseAsync<T>(
        this Task<Optional<T>> optionalTask,
        Func<Task<Optional<T>>> fallbackFactoryAsync)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.HasValue ? optional : await fallbackFactoryAsync();
    }
}
