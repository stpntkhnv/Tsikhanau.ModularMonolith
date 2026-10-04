namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Task<Result<T>> FirstSuccessAsync<T>(params ReadOnlySpan<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        return FirstSuccessWhenAllAsync(Task.WhenAll(resultTasks));
    }

    public static Task<Result<T>> FirstSuccessAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        Guard.AgainstNull(resultTasks);

        return FirstSuccessWhenAllAsync(Task.WhenAll(resultTasks));
    }

    private static async Task<Result<T>> FirstSuccessWhenAllAsync<T>(Task<Result<T>[]> whenAll)
        where T : notnull
    {
        var results = await whenAll;
        return FirstSuccess<T>(results);
    }
}
