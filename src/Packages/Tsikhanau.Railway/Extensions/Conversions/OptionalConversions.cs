using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static class OptionalConversions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this Optional<T> optional,
        Error error)
        where T : notnull
    {
        return optional.HasValue
            ? Result<T>.Success(optional.Value)
            : Result<T>.Failure(error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> ToResult<T>(
        this Optional<T> optional,
        Func<Error> errorFactory)
        where T : notnull
    {
        return optional.HasValue
            ? Result<T>.Success(optional.Value)
            : Result<T>.Failure(errorFactory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? AsNullable<T>(
        this Optional<T> optional)
        where T : struct
    {
        return optional.HasValue
            ? optional.Value
            : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> AsEnumerable<T>(
        this Optional<T> optional)
        where T : notnull
    {
        return optional.HasValue
            ? [optional.Value]
            : [];
    }
}
