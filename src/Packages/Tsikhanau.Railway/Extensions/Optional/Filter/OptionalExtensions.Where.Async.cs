namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    public static async Task<Optional<T>> WhereAsync<T>(
        this Task<Optional<T>> optionalTask,
        Func<T, Boolean> predicate)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.Where(predicate);
    }

    public static async Task<Optional<T>> WhereAsync<T>(
        this Task<Optional<T>> optionalTask,
        Func<T, Task<Boolean>> predicateAsync)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.HasValue && await predicateAsync(optional.Value)
            ? optional
            : Optional<T>.None();
    }

    public static async Task<Optional<T>> WhereAsync<T>(
        this Optional<T> optional,
        Func<T, Task<Boolean>> predicateAsync)
        where T : notnull
    {
        return optional.HasValue && await predicateAsync(optional.Value)
            ? optional
            : Optional<T>.None();
    }
}
