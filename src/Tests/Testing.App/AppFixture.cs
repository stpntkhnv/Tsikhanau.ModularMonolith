using Tsikhanau.Monolith.Testing.App;

namespace ModularMonolith.Testing.App;

public sealed class AppFixture : AppFixture<Program>
{
    protected override AppFactory CreateFactory(TimeProvider timeProvider, String environmentName) =>
        new(timeProvider, environmentName);
}
