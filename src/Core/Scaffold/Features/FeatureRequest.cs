using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record FeatureRequest(String ModuleName, String Name, FeatureKind Kind, EndpointMethod Method)
{
    private const String ListVerb = "List";

    private const String CommandAnsweringGet =
        "a command answers POST, PUT or DELETE, not GET: a GET only reads, "
        + "so give it a query (--kind query --endpoint get)";

    private const String ChangeIsCommand =
        "a change of state is a command (--kind command --endpoint post, put or delete)";

    private const String EventHandlerWithEndpoint =
        "an event handler has no endpoint: the module runs it when the event arrives";

    private const String InternalCommandWithEndpoint =
        "an internal command has no endpoint: a handler of the module schedules it and the queue runs it";

    public Optional<String> EventType { get; init; } = Optional<String>.None();

    public Optional<EndpointRoute> Route { get; init; } = Optional<EndpointRoute>.None();

    public Boolean IsQuery => Kind is FeatureKind.Query or FeatureKind.PublicQuery;

    private String QueryNamedList =>
        $"feature name '{Name}' starts with {ListVerb}: a query reads one item or a collection with Get, "
        + $"so name it Get{Name[ListVerb.Length..]}";

    private String PublicQueryWithEndpoint =>
        $"a public query has no endpoint: neighbours ask I{ModuleName}QueryFacade for it; "
        + "for HTTP add a query of the module (--kind query --endpoint get)";

    private String EventHandlerWithoutEvent =>
        $"--kind event-handler needs --event <Type>: a domain event of {ModuleName} "
        + "or an integration event of a neighbour";

    private String HttpMethodName => $"{Method}".ToUpperInvariant();

    private String KindDescription =>
        Kind switch
        {
            FeatureKind.Query => "a query",
            FeatureKind.PublicQuery => "a public query",
            FeatureKind.InternalCommand => "an internal command",
            _ => "a command",
        };

    public Result<FeatureRequest> Validate() =>
        FeatureName
            .Validate(Name)
            .Bind(_ => EnsureQueryIsNotNamedList())
            .Bind(_ => EnsureMethodFitsKind())
            .Bind(_ => EnsureEventFitsKind());

    private Result<FeatureRequest> EnsureQueryIsNotNamedList() =>
        IsQuery && Name.StartsWithWord(ListVerb) ? BuildError(QueryNamedList) : this;

    private Result<FeatureRequest> EnsureMethodFitsKind() =>
        (Kind, Method) switch
        {
            (FeatureKind.Command, EndpointMethod.Get) => BuildError(CommandAnsweringGet),
            (FeatureKind.Query, not (EndpointMethod.None or EndpointMethod.Get)) => BuildError(
                $"a query answers only GET, not {HttpMethodName}: {ChangeIsCommand}"),
            (FeatureKind.PublicQuery, not EndpointMethod.None) => BuildError(PublicQueryWithEndpoint),
            (FeatureKind.EventHandler, not EndpointMethod.None) => BuildError(EventHandlerWithEndpoint),
            (FeatureKind.InternalCommand, not EndpointMethod.None) => BuildError(InternalCommandWithEndpoint),
            _ => this,
        };

    private Result<FeatureRequest> EnsureEventFitsKind() =>
        (Kind == FeatureKind.EventHandler, EventType.HasValue) switch
        {
            (true, false) => BuildError(EventHandlerWithoutEvent),
            (false, true) => BuildError($"--event belongs to --kind event-handler: {KindDescription} handles no event"),
            _ => this,
        };

    private static Error BuildError(String message) => Error.Failure("scaffold.feature", message);
}
