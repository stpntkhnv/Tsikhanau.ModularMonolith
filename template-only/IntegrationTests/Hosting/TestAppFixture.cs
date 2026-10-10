using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

public sealed class TestAppFixture : AppFixture<Program>
{
    protected override AppFactory<Program> CreateFactory(TimeProvider timeProvider, String environmentName) =>
        new TestAppFactory(timeProvider, environmentName);
}
