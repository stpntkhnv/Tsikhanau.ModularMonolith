using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record FeatureRequest(String ModuleName, String Name, FeatureKind Kind, EndpointMethod Method)
{
    private const String CommandAnsweringGet =
        "a command answers POST, PUT or DELETE, not GET: a GET only reads, "
        + "so give it a query (--kind query --endpoint get)";

    private const String ChangeIsCommand =
        "a change of state is a command (--kind command --endpoint post, put or delete)";

    private String PublicQueryWithEndpoint =>
        $"a public query has no endpoint: neighbours ask I{ModuleName}QueryFacade for it; "
        + "for HTTP add a query of the module (--kind query --endpoint get)";

    private String HttpMethodName => $"{Method}".ToUpperInvariant();

    public Result<FeatureRequest> Validate() =>
        FeatureName
            .Validate(Name)
            .Bind(_ => EnsureMethodFitsKind());

    private Result<FeatureRequest> EnsureMethodFitsKind() =>
        (Kind, Method) switch
        {
            (FeatureKind.Command, EndpointMethod.Get) => BuildError(CommandAnsweringGet),
            (FeatureKind.Query, not (EndpointMethod.None or EndpointMethod.Get)) => BuildError(
                $"a query answers only GET, not {HttpMethodName}: {ChangeIsCommand}"),
            (FeatureKind.PublicQuery, not EndpointMethod.None) => BuildError(PublicQueryWithEndpoint),
            _ => this,
        };

    private static Error BuildError(String message) => Error.Failure("scaffold.feature", message);
}
