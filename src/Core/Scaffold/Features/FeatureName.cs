using System.Text.RegularExpressions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal static partial class FeatureName
{
    private const String PascalCaseRule = "must be PascalCase: an ASCII capital letter, then letters and digits";

    private const String AddedSuffixRule = "scaffold adds the suffix of each type itself";

    private static readonly String[] AddedSuffixes =
        ["Command", "Handler", "Request", "Endpoint", "Tests", "Query", "Dto", "Event"];

    [GeneratedRegex(@"\A[A-Z][A-Za-z0-9]*\z")]
    private static partial Regex PascalCasePattern { get; }

    public static Result<String> Validate(String name) =>
        Result
            .Success(name)
            .Ensure(IsPascalCase, BuildError(name, PascalCaseRule))
            .Bind(EnsureNoAddedSuffix);

    private static Boolean IsPascalCase(String name) => PascalCasePattern.IsMatch(name);

    private static Result<String> EnsureNoAddedSuffix(String name) =>
        FindAddedSuffix(name)
            .Match(
                suffix => BuildError(name, $"must not end in {suffix}: {AddedSuffixRule}"),
                () => Result.Success(name));

    private static Optional<String> FindAddedSuffix(String name) =>
        AddedSuffixes.FirstOrDefault(suffix => name.EndsWith(suffix, StringComparison.Ordinal));

    private static Error BuildError(String name, String rule) =>
        Error.Failure("scaffold.name", $"feature name '{name}' {rule}");
}
