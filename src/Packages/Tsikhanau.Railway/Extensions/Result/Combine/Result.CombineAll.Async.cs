namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Task<Result<IReadOnlyList<T>>> CombineAllAsync<T>(params ReadOnlySpan<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        return CombineAllWhenAllAsync(Task.WhenAll(resultTasks));
    }

    public static Task<Result<IReadOnlyList<T>>> CombineAllAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        Guard.AgainstNull(resultTasks);

        return CombineAllWhenAllAsync(Task.WhenAll(resultTasks));
    }

    private static async Task<Result<IReadOnlyList<T>>> CombineAllWhenAllAsync<T>(Task<Result<T>[]> whenAll)
        where T : notnull
    {
        var results = await whenAll;
        return CombineAll<T>(results);
    }
}
