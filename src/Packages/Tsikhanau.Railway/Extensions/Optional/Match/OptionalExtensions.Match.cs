using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult Match<T, TResult>(
        this Optional<T> optional,
        Func<T, TResult> onSome,
        Func<TResult> onNone)
        where T : notnull
    {
        return optional.HasValue
            ? onSome(optional.Value)
            : onNone();
    }
}
