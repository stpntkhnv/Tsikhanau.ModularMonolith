using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Tsikhanau.Monolith.Analyzers;

internal static class RuleLines
{
    private const String ErrorsAsValues =
        "Errors are values: Domain and Features return an `Error` for an expected failure and never throw.";

    private const String ClrTypeNames = "CLR type names (`String`, `Int32`, `Boolean`), not C# keywords.";

    private const String LinesAndStatements =
        "Lines fit in 120 characters, a chain of two calls or more puts each call on its own line, and a statement "
        + "that spans several lines is followed by a blank line unless the block ends there.";

    private const String DependencyNames =
        "A constructor dependency is named after its type: `INoteRepository noteRepository`, "
        + "`TimeProvider timeProvider`.";

    private const String ModuleSql =
        "Module SQL names its columns and never another module's schema; module code opens no database connection "
        + "of its own. SQL passed to Dapper or EF Core raw SQL is a constant, values go in parameters.";

    private static readonly RuleLine[] All =
    [
        new(ErrorsAsValues, "add-feature", "TS0005"),
        new(ClrTypeNames, "", "TS0004"),
        new(LinesAndStatements, "", "TS0001", "TS0002"),
        new(DependencyNames, "", "TS0003"),
        new(ModuleSql, "add-feature", "TS0006"),
    ];

    public static DiagnosticDescriptor Explain(DiagnosticDescriptor descriptor) =>
        new(
            descriptor.Id,
            descriptor.Title,
            All
                .Single(line => line.Covers(descriptor.Id))
                .Explain(descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)),
            descriptor.Category,
            descriptor.DefaultSeverity,
            descriptor.IsEnabledByDefault);
}
