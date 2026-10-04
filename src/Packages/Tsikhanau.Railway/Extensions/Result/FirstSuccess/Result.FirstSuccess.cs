namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Result<T> FirstSuccess<T>(params ReadOnlySpan<Result<T>> results)
        where T : notnull
    {
        if (results.IsEmpty)
        {
            throw new ArgumentException("At least one result is required.", nameof(results));
        }

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                return result;
            }
        }

        return results[^1];
    }

    public static Result<T> FirstSuccess<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        Guard.AgainstNull(results);

        var any = false;
        var last = default(Result<T>);
        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                return result;
            }

            any = true;
            last = result;
        }

        return any
            ? last
            : throw new ArgumentException("At least one result is required.", nameof(results));
    }
}
