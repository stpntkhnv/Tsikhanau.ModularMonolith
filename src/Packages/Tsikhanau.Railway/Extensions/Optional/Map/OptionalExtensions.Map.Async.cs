namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    public static async Task<Optional<TResult>> MapAsync<T, TResult>(
        this Task<Optional<T>> optionalTask,
        Func<T, TResult?> mapper)
        where T : notnull
        where TResult : notnull
    {
        var optional = await optionalTask;
        return optional.Map(mapper);
    }

    public static async Task<Optional<TResult>> MapAsync<T, TResult>(
        this Task<Optional<T>> optionalTask,
        Func<T, Task<TResult>> mapperAsync)
        where T : notnull
        where TResult : notnull
    {
        var optional = await optionalTask;
        return optional.HasValue
            ? Optional<TResult>.FromNullable(await mapperAsync(optional.Value))
            : Optional<TResult>.None();
    }

    public static async Task<Optional<TResult>> MapAsync<T, TResult>(
        this Optional<T> optional,
        Func<T, Task<TResult>> mapperAsync)
        where T : notnull
        where TResult : notnull
    {
        return optional.HasValue
            ? Optional<TResult>.FromNullable(await mapperAsync(optional.Value))
            : Optional<TResult>.None();
    }
}
