using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalConversions
{
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
