using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record FeatureRequest(String ModuleName, String Name, FeatureKind Kind, EndpointMethod Method)
{
    private const String CommandAnsweringGet = "a command answers POST, PUT or DELETE, not GET: a GET only reads";

    public Result<FeatureRequest> Validate() =>
        FeatureName
            .Validate(Name)
            .Bind(_ => EnsureMethodFitsKind());

    private Result<FeatureRequest> EnsureMethodFitsKind() =>
        (Kind, Method) switch
        {
            (FeatureKind.Command, EndpointMethod.Get) => Error.Failure("scaffold.feature", CommandAnsweringGet),
            _ => this,
        };
}
