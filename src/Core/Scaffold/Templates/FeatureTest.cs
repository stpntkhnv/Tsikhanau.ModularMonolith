namespace Tsikhanau.Monolith.Scaffold.Templates;

internal sealed record FeatureTest(
    String Method,
    IReadOnlyList<String> Usings,
    Boolean UsesCancellationToken,
    String Body);
