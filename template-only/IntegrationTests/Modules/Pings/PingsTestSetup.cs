using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Modules.Pings;

internal sealed class PingsTestSetup : IModuleTestSetup
{
    public IReadOnlyDictionary<String, String> Settings { get; } =
        new Dictionary<String, String> { ["App:Modules:Pings:Limit"] = "10" };
}
