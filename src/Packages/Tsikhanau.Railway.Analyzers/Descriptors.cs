using Microsoft.CodeAnalysis;

namespace Tsikhanau.Railway.Analyzers;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor ResultNotUsed = new(
        DiagnosticIds.ResultNotUsed,
        "Result is not used",
        "The Result returned by '{0}' is not used; handle it or discard it explicitly with '_ ='",
        "Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A failed Result that is not used loses its error silently.");

    public static readonly DiagnosticDescriptor MapReturnsResult = new(
        DiagnosticIds.MapReturnsResult,
        "Use Bind when the mapper returns a Result",
        "Use '{1}' instead of '{0}': the mapper returns a Result, so '{0}' produces a nested Result",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Map with a mapper that returns a Result produces Result<Result<T>>; Bind flattens it.");

    public static readonly DiagnosticDescriptor UncheckedResultAccess = new(
        DiagnosticIds.UncheckedResultAccess,
        "Result value or error is accessed without a check",
        "Result.{0} is accessed without checking Result.{1} first",
        "Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Result.Value throws on a failed result and Result.Error throws on a successful one.");
}
