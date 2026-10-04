using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<TResult> Map<T, TResult>(
        this Optional<T> optional,
        Func<T, TResult?> mapper)
        where T : notnull
        where TResult : notnull
    {
        return optional.HasValue
            ? Optional<TResult>.FromNullable(mapper(optional.Value))
            : Optional<TResult>.None();
    }
}
