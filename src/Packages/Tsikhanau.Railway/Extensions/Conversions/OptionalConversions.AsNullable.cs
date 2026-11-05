using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalConversions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? AsNullable<T>(
        this Optional<T> optional)
        where T : struct
    {
        return optional.HasValue
            ? optional.Value
            : null;
    }
}
