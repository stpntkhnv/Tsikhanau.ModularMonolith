using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosts.TestApp;

public sealed class TestAppFixture : AppFixture<Program>
{
    protected override TestAppFactory CreateFactory(TimeProvider timeProvider, String environmentName) =>
        new(timeProvider, environmentName);
}
