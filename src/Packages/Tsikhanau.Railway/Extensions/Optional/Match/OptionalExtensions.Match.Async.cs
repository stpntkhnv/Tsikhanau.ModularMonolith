namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Optional<T>> optionalTask,
        Func<T, TResult> onSome,
        Func<TResult> onNone)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.Match(onSome, onNone);
    }

    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Optional<T>> optionalTask,
        Func<T, Task<TResult>> onSomeAsync,
        Func<Task<TResult>> onNoneAsync)
        where T : notnull
    {
        var optional = await optionalTask;
        return optional.HasValue
            ? await onSomeAsync(optional.Value)
            : await onNoneAsync();
    }

    public static async Task<TResult> MatchAsync<T, TResult>(
        this Optional<T> optional,
        Func<T, Task<TResult>> onSomeAsync,
        Func<Task<TResult>> onNoneAsync)
        where T : notnull
    {
        return optional.HasValue
            ? await onSomeAsync(optional.Value)
            : await onNoneAsync();
    }
}
