using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.App;

public sealed class AppFixtureTests
{
    private const String SetupName = "set in the derived constructor";

    [Fact]
    public async Task Factory_override_reads_state_set_in_the_derived_constructor()
    {
        await using var setupFixture = new SetupFixture(SetupName);

        var testSetup = setupFixture.Services.GetRequiredService<TestSetup>();

        testSetup.Name.ShouldBe(SetupName);
    }

    private sealed record TestSetup(String Name);

    private sealed class SetupFixture : AppFixture<AppFixtureTests>
    {
        private readonly String _setupName;

        public SetupFixture(String setupName)
        {
            _setupName = setupName;
        }

        protected override AppFactory<AppFixtureTests> CreateFactory(
            TimeProvider timeProvider,
            String environmentName) =>
            new SetupFactory(timeProvider, environmentName, new TestSetup(_setupName));
    }

    private sealed class SetupFactory(TimeProvider timeProvider, String environmentName, TestSetup testSetup)
        : AppFactory<AppFixtureTests>(timeProvider, environmentName)
    {
        public override IServiceProvider Services { get; } =
            new ServiceCollection()
                .AddSingleton(testSetup)
                .BuildServiceProvider();
    }
}
