using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

public static partial class OptionalExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Optional<TResult> Bind<T, TResult>(
        this Optional<T> optional,
        Func<T, Optional<TResult>> binder)
        where T : notnull
        where TResult : notnull
    {
        return optional.HasValue
            ? binder(optional.Value)
            : Optional<TResult>.None();
    }
}
