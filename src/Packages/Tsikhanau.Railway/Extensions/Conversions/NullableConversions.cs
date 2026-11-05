using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static class NullableConversions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> AsOptional<T>(
        this T? value)
        where T : struct
    {
        return value.HasValue
            ? Optional<T>.Some(value.Value)
            : Optional<T>.None();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> AsOptional<T>(
        this T? value)
        where T : class
    {
        return Optional<T>.FromNullable(value);
    }
}
