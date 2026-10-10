using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

public sealed class TestAppFixture : AppFixture<Program>
{
    private static readonly IReadOnlyDictionary<String, String> TestModuleSettings =
        new Dictionary<String, String>
        {
            ["App:Modules:Pings:Limit"] = "10",
            ["App:Modules:Pongs:Limit"] = "10",
        };

    protected override AppFactory<Program> CreateFactory(TimeProvider timeProvider, String environmentName) =>
        new TestAppFactory(timeProvider, environmentName, TestModuleSettings);
}
