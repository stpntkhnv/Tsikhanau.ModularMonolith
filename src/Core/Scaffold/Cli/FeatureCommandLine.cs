using System.Collections.Immutable;
using Tsikhanau.Monolith.Scaffold.Features;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Cli;

internal static class FeatureCommandLine
{
    public const String Synopsis = $"bash scripts/scaffold.sh feature <Module> <Name> {KindSynopsis} {OptionsSynopsis}";

    private const String KindSynopsis = "--kind command|query|public-query|event-handler|internal-command";

    private const String OptionsSynopsis =
        $"[{EndpointOption} none|get|post|put|delete] [{RouteOption} <path>] [{EventOption} <Type>]";

    private const String KindOption = "--kind";

    private const String EndpointOption = "--endpoint";

    private const String RouteOption = "--route";

    private const String EventOption = "--event";

    private const String RouteWithoutEndpoint =
        $"{RouteOption} needs an endpoint: {EndpointOption} get, post, put or delete";

    private static readonly String[] Options = [KindOption, EndpointOption, RouteOption, EventOption];

    private static readonly Dictionary<String, FeatureKind> Kinds = new(StringComparer.Ordinal)
    {
        ["command"] = FeatureKind.Command,
        ["query"] = FeatureKind.Query,
        ["public-query"] = FeatureKind.PublicQuery,
        ["event-handler"] = FeatureKind.EventHandler,
        ["internal-command"] = FeatureKind.InternalCommand,
    };

    private static readonly Dictionary<String, EndpointMethod> Methods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = EndpointMethod.None,
        ["get"] = EndpointMethod.Get,
        ["post"] = EndpointMethod.Post,
        ["put"] = EndpointMethod.Put,
        ["delete"] = EndpointMethod.Delete,
    };

    public static Result<IScaffold> Parse(IReadOnlyList<String> arguments) =>
        ParseNamesAndOptions(arguments)
            .MapError(error => Error.Failure(error.Code, $"{error.Message}; usage: {Synopsis}"));

    private static Result<IScaffold> ParseNamesAndOptions(IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [var moduleName, var featureName, ..] when AreNames(moduleName, featureName) =>
                ParseOptions(ImmutableDictionary<String, String>.Empty, [.. arguments.Skip(2)])
                    .Bind(options => CreateScaffold(moduleName, featureName, options)),
            _ => CommandLineWords.BuildUsageError("feature takes a module name and a feature name, then its options"),
        };

    private static Boolean AreNames(String moduleName, String featureName) =>
        CommandLineWords.IsName(moduleName) && CommandLineWords.IsName(featureName);

    private static Result<ImmutableDictionary<String, String>> ParseOptions(
        ImmutableDictionary<String, String> options,
        IReadOnlyList<String> arguments) =>
        arguments switch
        {
            [] => options,
            [var option, ..] when !Options.Contains(option) =>
                CommandLineWords.BuildUsageError($"feature does not know '{option}'"),
            [var option, ..] when options.ContainsKey(option) =>
                CommandLineWords.BuildUsageError($"{option} is given twice"),
            [var option, var value, ..] when CommandLineWords.IsName(value) =>
                ParseOptions(options.Add(option, value), [.. arguments.Skip(2)]),
            [var option, ..] => CommandLineWords.BuildUsageError($"{option} needs a value"),
        };

    private static Result<IScaffold> CreateScaffold(
        String moduleName,
        String featureName,
        IReadOnlyDictionary<String, String> options) =>
        Result
            .Collect(ParseKind(options))
            .And(ParseMethod(options))
            .And(ParseRoute(options))
            .Bind((kind, method, route) => RequireEndpointForRoute(
                new FeatureRequest(moduleName, featureName, kind, method)
                {
                    EventType = FindEvent(options),
                    Route = route,
                }));

    private static Result<IScaffold> RequireEndpointForRoute(FeatureRequest request) =>
        HasRouteWithoutEndpoint(request)
            ? CommandLineWords.BuildUsageError(RouteWithoutEndpoint)
            : Result.Success<IScaffold>(new FeatureScaffold(request));

    private static Boolean HasRouteWithoutEndpoint(FeatureRequest request) =>
        request.Route.HasValue && request.Method == EndpointMethod.None;

    private static Optional<String> FindEvent(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(EventOption, out var eventType) ? eventType : Optional<String>.None();

    private static Result<FeatureKind> ParseKind(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(KindOption, out var kind)
            ? ParseValue(Kinds, kind, $"{KindOption} '{kind}' is not a feature kind")
            : CommandLineWords.BuildUsageError($"feature needs {KindOption}");

    private static Result<EndpointMethod> ParseMethod(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(EndpointOption, out var method)
            ? ParseValue(Methods, method, $"{EndpointOption} '{method}' is not an endpoint method")
            : EndpointMethod.None;

    private static Result<Optional<EndpointRoute>> ParseRoute(IReadOnlyDictionary<String, String> options) =>
        options.TryGetValue(RouteOption, out var route)
            ? EndpointRoute
                .Parse(route)
                .Map(Optional<EndpointRoute>.Some)
            : Optional<EndpointRoute>.None();

    private static Result<T> ParseValue<T>(IReadOnlyDictionary<String, T> values, String name, String unknown)
        where T : notnull =>
        values.TryGetValue(name, out var value) ? value : CommandLineWords.BuildUsageError(unknown);
}
