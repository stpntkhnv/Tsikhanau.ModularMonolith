using Tsikhanau.Monolith.Scaffold.Plans;

namespace Tsikhanau.Monolith.Scaffold.Features;

internal sealed record FeatureFiles(IReadOnlyList<NewFile> NewFiles, String TestMethod);
