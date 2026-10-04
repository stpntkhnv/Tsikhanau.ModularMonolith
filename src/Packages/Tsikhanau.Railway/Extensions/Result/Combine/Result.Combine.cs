namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Result<IReadOnlyList<T>> Combine<T>(params ReadOnlySpan<Result<T>> results)
        where T : notnull
    {
        if (results.IsEmpty)
        {
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());
        }

        var values = new T[results.Length];
        for (var i = 0; i < results.Length; i++)
        {
            var result = results[i];
            if (result.IsFailure)
            {
                return Result<IReadOnlyList<T>>.Failure(result.Error);
            }

            values[i] = result.Value;
        }

        return Result<IReadOnlyList<T>>.Success(values);
    }

    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        Guard.AgainstNull(results);

        var values = results.TryGetNonEnumeratedCount(out var count) ? new List<T>(count) : new List<T>();
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Result<IReadOnlyList<T>>.Failure(result.Error);
            }

            values.Add(result.Value);
        }

        return Result<IReadOnlyList<T>>.Success(values);
    }

    public static Result<Unit> CombineIgnoreValues<T>(params ReadOnlySpan<Result<T>> results)
        where T : notnull
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Result<Unit>.Failure(result.Error);
            }
        }

        return Result<Unit>.Success(Unit.Value);
    }

    public static Result<Unit> CombineIgnoreValues<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        Guard.AgainstNull(results);

        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Result<Unit>.Failure(result.Error);
            }
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
