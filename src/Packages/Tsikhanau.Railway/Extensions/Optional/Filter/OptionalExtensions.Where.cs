using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> Where<T>(
        this Optional<T> optional,
        Func<T, Boolean> predicate)
        where T : notnull
    {
        return optional.HasValue && predicate(optional.Value)
            ? optional
            : Optional<T>.None();
    }
}
