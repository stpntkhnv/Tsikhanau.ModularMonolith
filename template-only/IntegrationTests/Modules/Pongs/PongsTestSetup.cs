using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Modules.Pongs;

internal sealed class PongsTestSetup : IModuleTestSetup
{
    public IReadOnlyDictionary<String, String> Settings { get; } =
        new Dictionary<String, String> { ["App:Modules:Pongs:Limit"] = "10" };
}
