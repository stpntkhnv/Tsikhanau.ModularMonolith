using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static class ResultConversions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<T> ToOptional<T>(
        this Result<T> result)
        where T : notnull
    {
        return result.IsSuccess
            ? Optional<T>.Some(result.Value)
            : Optional<T>.None();
    }
}
