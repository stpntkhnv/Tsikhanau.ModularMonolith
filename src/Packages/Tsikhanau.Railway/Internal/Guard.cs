using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tsikhanau.Railway;

internal static class Guard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AgainstNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] String paramName = "")
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String AgainstNullOrWhiteSpace([NotNull] String? value, [CallerArgumentExpression(nameof(value))] String paramName = "")
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        if (String.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AgainstUncollected(Boolean initialized)
    {
        if (!initialized)
            throw new InvalidOperationException("Collected is not initialized. Start it with Result.Collect.");
    }
}
