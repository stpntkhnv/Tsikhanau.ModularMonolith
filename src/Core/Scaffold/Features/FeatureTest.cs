namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record FeatureTest(
    String Method,
    IReadOnlyList<String> Usings,
    Boolean UsesCancellationToken,
    String Body);
