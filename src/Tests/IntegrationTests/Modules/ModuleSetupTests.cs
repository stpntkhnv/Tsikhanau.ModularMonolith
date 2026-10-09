using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ModularMonolith.Testing.App;
using ModularMonolith.Testing.Modules;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.IntegrationTests.Modules;

public sealed class ModuleSetupTests(AppFixture appFixture) : AppTest(appFixture)
{
    private static readonly String[] CommonTables =
        ["__ef_migrations_history", "inbox_messages", "internal_commands", "outbox_messages"];

    private static readonly Dictionary<String, String[]> OwnTables = [];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_module_folder_has_exactly_one_registered_module()
    {
        var registeredModuleNames = App.Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Select(module => module.Name)
            .ToList();

        Assert.SkipWhen(
            ProductionModules.Names.Length == 0 && registeredModuleNames.Count == 0,
            "There are no production modules");

        registeredModuleNames.ShouldBe(ProductionModules.Names, ignoreOrder: true);
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Module_facade_is_registered(String name)
    {
        var moduleFacade = App.Services.GetService(App.GetModule(name).Key);

        moduleFacade.ShouldNotBeNull();
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Query_facade_is_the_module_facade(String name)
    {
        var module = App.GetModule(name);

        var queryFacade = App.Services.GetService(module.QueryFacadeKey);
        var moduleFacade = App.Services.GetService(module.Key);

        queryFacade.ShouldNotBeNull();
        queryFacade.ShouldBeSameAs(moduleFacade);
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public void Module_settings_are_validated_with_data_annotations(String name)
    {
        var foundSettingsType = FindSettingsType(App.GetModule(name));
        Assert.SkipWhen(foundSettingsType.IsNone, $"Module {name} has no settings type");
        var settingsType = foundSettingsType.Value;

        var validatorTypes = GetValidatorTypes(settingsType);

        validatorTypes.ShouldContain(typeof(DataAnnotationValidateOptions<>).MakeGenericType(settingsType));
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public async Task Module_schema_holds_exactly_the_queues_and_its_own_tables(String name)
    {
        var tableNames = await App.Database.ReadTableNamesAsync(App.GetModule(name).Schema);

        String[] expectedTableNames = [.. CommonTables, .. OwnTables.GetValueOrDefault(name, [])];

        tableNames.ShouldBe(expectedTableNames, ignoreOrder: true);
    }

    [Theory(SkipTestWithoutData = true)]
    [EveryProductionModule]
    public async Task Module_database_health_check_is_healthy(String name)
    {
        var healthCheckName = $"database-{App.GetModule(name).Schema}";
        var healthCheckService = App.Services.GetRequiredService<HealthCheckService>();

        var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken);

        healthReport.Entries.ShouldContainKey(healthCheckName);
        healthReport.Entries[healthCheckName].Status.ShouldBe(HealthStatus.Healthy);
    }

    private static Optional<Type> FindSettingsType(ModuleDescriptor module)
    {
        var settingsTypeName = $"{module.Key.Namespace}.Settings.{module.Name}Settings";

        return Optional<Type>.FromNullable(module.Key.Assembly.GetType(settingsTypeName));
    }

    private List<Type> GetValidatorTypes(Type settingsType) =>
        App.Services
            .GetServices(typeof(IValidateOptions<>).MakeGenericType(settingsType))
            .OfType<Object>()
            .Select(validator => validator.GetType())
            .ToList();
}
