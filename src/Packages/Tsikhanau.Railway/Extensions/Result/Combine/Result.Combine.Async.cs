namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Task<Result<IReadOnlyList<T>>> CombineAsync<T>(params ReadOnlySpan<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        return CombineWhenAllAsync(Task.WhenAll(resultTasks));
    }

    public static Task<Result<IReadOnlyList<T>>> CombineAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        Guard.AgainstNull(resultTasks);

        return CombineWhenAllAsync(Task.WhenAll(resultTasks));
    }

    public static Task<Result<Unit>> CombineIgnoreValuesAsync<T>(params ReadOnlySpan<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        return CombineIgnoreValuesWhenAllAsync(Task.WhenAll(resultTasks));
    }

    public static Task<Result<Unit>> CombineIgnoreValuesAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks)
        where T : notnull
    {
        Guard.AgainstNull(resultTasks);

        return CombineIgnoreValuesWhenAllAsync(Task.WhenAll(resultTasks));
    }

    private static async Task<Result<IReadOnlyList<T>>> CombineWhenAllAsync<T>(Task<Result<T>[]> whenAll)
        where T : notnull
    {
        var results = await whenAll;
        return Combine<T>(results);
    }

    private static async Task<Result<Unit>> CombineIgnoreValuesWhenAllAsync<T>(Task<Result<T>[]> whenAll)
        where T : notnull
    {
        var results = await whenAll;
        return CombineIgnoreValues<T>(results);
    }
}
