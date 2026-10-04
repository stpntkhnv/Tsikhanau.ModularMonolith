using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> Tap<T>(
        this Optional<T> optional,
        Action<T> action)
        where T : notnull
    {
        if (optional.HasValue)
        {
            action(optional.Value);
        }

        return optional;
    }
}
