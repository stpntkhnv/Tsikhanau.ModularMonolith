namespace Tsikhanau.Railway;

public static partial class Result
{
    public static Result<IReadOnlyList<T>> CombineAll<T>(params ReadOnlySpan<Result<T>> results)
        where T : notnull
    {
        if (results.IsEmpty)
        {
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());
        }

        var values = new T[results.Length];
        List<Error>? errors = null;
        for (var i = 0; i < results.Length; i++)
        {
            var result = results[i];
            if (result.IsFailure)
            {
                (errors ??= []).Add(result.Error);
            }
            else
            {
                values[i] = result.Value;
            }
        }

        return errors is null
            ? Result<IReadOnlyList<T>>.Success(values)
            : Result<IReadOnlyList<T>>.Failure(ErrorMerge.Merge(errors));
    }

    public static Result<IReadOnlyList<T>> CombineAll<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        Guard.AgainstNull(results);

        var values = results.TryGetNonEnumeratedCount(out var count) ? new List<T>(count) : new List<T>();
        List<Error>? errors = null;
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                (errors ??= []).Add(result.Error);
            }
            else
            {
                values.Add(result.Value);
            }
        }

        return errors is null
            ? Result<IReadOnlyList<T>>.Success(values)
            : Result<IReadOnlyList<T>>.Failure(ErrorMerge.Merge(errors));
    }
}
