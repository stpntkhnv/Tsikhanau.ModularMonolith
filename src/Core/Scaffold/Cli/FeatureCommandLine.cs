using System.Collections.Immutable;
using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class FeatureCommandLine
{
    public const String Synopsis =
        "bash scripts/scaffold.sh feature <Module> <Name> --kind command [--endpoint none|post|put|delete]";

    private const String KindOption = "--kind";

    private const String EndpointOption = "--endpoint";

    private static readonly String[] Options = [KindOption, EndpointOption];

    private static readonly Dictionary<String, FeatureKind> Kinds = new(StringComparer.Ordinal)
    {
        ["command"] = FeatureKind.Command,
    };

    private static readonly Dictionary<String, EndpointMethod> Methods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = EndpointMethod.None,
        ["get"] = EndpointMethod.Get,
        ["post"] = EndpointMethod.Post,
        ["put"] = EndpointMethod.Put,
        ["delete"] = EndpointMethod.Delete,
    };

    public static Result<IScaffoldCommand> Parse(IReadOnlyList<String> arguments) =>
        ParseNamesAndOptions(arguments)
            .MapError(error => Error.Failure(error.Code, $"{error.Message}; usage: {Synopsis}"));

    private static Result<IScaffoldCommand> ParseNamesAndOptions(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [var moduleName, var featureName, ..] when AreNames(moduleName, featureName) =>
                ParseOptions(ImmutableDictionary<String, String>.Empty, [.. arguments.Skip(2)])
                    .Bind(options => CreateCommand(moduleName, featureName, options)),
            _ => BuildUsageError("feature takes a module name and a feature name, then its options"),
        };

    private static Boolean AreNames(String moduleName, String featureName) =>
        ScaffoldCommandLine.IsName(moduleName) && ScaffoldCommandLine.IsName(featureName);

    private static Result<ImmutableDictionary<String, String>> ParseOptions(
        ImmutableDictionary<String, String> options,
        IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [] => options,
            [var option, ..] when !Options.Contains(option) => BuildUsageError($"feature does not know '{option}'"),
            [var option, ..] when options.ContainsKey(option) => BuildUsageError($"{option} is given twice"),
            [var option, var value, ..] when ScaffoldCommandLine.IsName(value) =>
                ParseOptions(options.Add(option, value), [.. arguments.Skip(2)]),
            [var option, ..] => BuildUsageError($"{option} needs a value"),
        };

    private static Result<IScaffoldCommand> CreateCommand(
        String moduleName,
        String featureName,
        IReadOnlyDictionary<String, String> options) =>
        Result
            .Collect(ParseKind(options))
            .And(ParseMethod(options))
            .Map(IScaffoldCommand (kind, method) =>
                new FeatureCommand(new FeatureRequest(moduleName, featureName, kind, method)));

    private static Result<FeatureKind> ParseKind(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(KindOption, out var kind)
            ? ParseValue(Kinds, kind, $"{KindOption} '{kind}' is not a feature kind")
            : BuildUsageError($"feature needs {KindOption}");

    private static Result<EndpointMethod> ParseMethod(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(EndpointOption, out var method)
            ? ParseValue(Methods, method, $"{EndpointOption} '{method}' is not an endpoint method")
            : EndpointMethod.None;

    private static Result<T> ParseValue<T>(IReadOnlyDictionary<String, T> values, String name, String unknown)
        where T : notnull =>
        values.TryGetValue(name, out var value) ? value : BuildUsageError(unknown);

    private static Error BuildUsageError(String message) => Error.Failure("scaffold.usage", message);
}
