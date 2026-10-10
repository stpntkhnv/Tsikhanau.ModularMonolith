using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Tsikhanau.Monolith.Testing.App;
using Tsikhanau.Monolith.TestModules.Pings.Settings;
using Tsikhanau.Monolith.TestModules.Pongs.Settings;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class ModuleTestSetupTests(TestAppFixture testAppFixture) : TestAppTest(testAppFixture)
{
    private const String SetupOrderKey = "App:SetupOrder";

    private const String PingsLimitKey = "App:Modules:Pings:Limit";

    [Fact]
    public void Setups_configure_services_in_the_order_of_their_full_type_names()
    {
        var setupNames = ResolveSetupNames(App.Services);

        setupNames.ShouldBe([nameof(AlphaSetup), nameof(ZuluSetup)]);
    }

    [Fact]
    public void Key_set_by_two_setups_takes_the_value_of_the_later_full_type_name()
    {
        var setupOrder = App.Services.GetRequiredService<IConfiguration>()[SetupOrderKey];

        setupOrder.ShouldBe(nameof(ZuluSetup));
    }

    [Fact]
    public void Module_setups_give_the_test_modules_their_required_settings()
    {
        var limits = ResolveLimits(App.Services);

        limits.ShouldBe(new Limits(PingsLimit: 10, PongsLimit: 10));
    }

    [Fact]
    public async Task Second_factory_gets_the_setups_too()
    {
        await using var appFactory = App.StartFactory(Environments.Development, new Dictionary<String, String>());

        var setupNames = ResolveSetupNames(appFactory.Services);
        var limits = ResolveLimits(appFactory.Services);

        setupNames.ShouldBe([nameof(AlphaSetup), nameof(ZuluSetup)]);
        limits.ShouldBe(new Limits(PingsLimit: 10, PongsLimit: 10));
    }

    [Fact]
    public async Task Override_of_a_test_wins_over_a_setup()
    {
        await using var appFactory = App.StartFactory(
            Environments.Development,
            new Dictionary<String, String> { [PingsLimitKey] = "20" });

        var limits = ResolveLimits(appFactory.Services);

        limits.ShouldBe(new Limits(PingsLimit: 20, PongsLimit: 10));
    }

    private static IEnumerable<String> ResolveSetupNames(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetServices<SetupCall>()
            .Select(setupCall => setupCall.SetupName);

    private static Limits ResolveLimits(IServiceProvider serviceProvider) =>
        new(
            serviceProvider.GetRequiredService<IOptions<PingsSettings>>().Value.Limit,
            serviceProvider.GetRequiredService<IOptions<PongsSettings>>().Value.Limit);

    private sealed record Limits(Int32 PingsLimit, Int32 PongsLimit);

    private sealed record SetupCall(String SetupName);

    private sealed class ZuluSetup : IModuleTestSetup
    {
        public IReadOnlyDictionary<String, String> Settings { get; } =
            new Dictionary<String, String> { [SetupOrderKey] = nameof(ZuluSetup) };

        public void ConfigureServices(IServiceCollection services) =>
            services.AddSingleton(new SetupCall(nameof(ZuluSetup)));
    }

    private sealed class AlphaSetup : IModuleTestSetup
    {
        public IReadOnlyDictionary<String, String> Settings { get; } =
            new Dictionary<String, String> { [SetupOrderKey] = nameof(AlphaSetup) };

        public void ConfigureServices(IServiceCollection services) =>
            services.AddSingleton(new SetupCall(nameof(AlphaSetup)));
    }
}
