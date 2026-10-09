using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Testing.Modules;

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

        registeredModuleNames.ShouldBe(ProductionModules.Names, ignoreOrder: true);
    }

    [Theory]
    [EveryProductionModule]
    public void Module_facade_is_registered(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        var moduleFacade = App.Services.GetService(App.GetModule(name).Key);

        moduleFacade.ShouldNotBeNull();
    }

    [Theory]
    [EveryProductionModule]
    public void Query_facade_is_the_module_facade(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        var module = App.GetModule(name);

        var queryFacade = App.Services.GetService(module.QueryFacadeKey);
        var moduleFacade = App.Services.GetService(module.Key);

        queryFacade.ShouldNotBeNull();
        queryFacade.ShouldBeSameAs(moduleFacade);
    }

    [Theory]
    [EveryProductionModule]
    public void Module_settings_are_validated_with_data_annotations(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        var settingsTypesWithoutValidation = GetSettingsTypes(App.GetModule(name))
            .Where(settingsType => !IsValidatedWithDataAnnotations(settingsType))
            .ToList();

        settingsTypesWithoutValidation.ShouldBeEmpty();
    }

    [Theory]
    [EveryProductionModule]
    public async Task Module_schema_holds_exactly_the_queues_and_its_own_tables(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        var tableNames = await App.Database.ReadTableNamesAsync(App.GetModule(name).Schema);

        String[] expectedTableNames = [.. CommonTables, .. OwnTables.GetValueOrDefault(name, [])];

        tableNames.ShouldBe(expectedTableNames, ignoreOrder: true);
    }

    [Theory]
    [EveryProductionModule]
    public async Task Module_database_health_check_is_healthy(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        var healthCheckName = $"database-{App.GetModule(name).Schema}";
        var healthCheckService = App.Services.GetRequiredService<HealthCheckService>();

        var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken);

        healthReport.Entries.ShouldContainKey(healthCheckName);
        healthReport.Entries[healthCheckName].Status.ShouldBe(HealthStatus.Healthy);
    }

    private static IEnumerable<Type> GetSettingsTypes(ModuleDescriptor module) =>
        module.Key.Assembly
            .GetTypes()
            .Where(type => type.FullName == $"{module.Key.Namespace}.Settings.{module.Name}Settings");

    private Boolean IsValidatedWithDataAnnotations(Type settingsType) =>
        GetValidatorTypes(settingsType)
            .Contains(typeof(DataAnnotationValidateOptions<>).MakeGenericType(settingsType));

    private List<Type> GetValidatorTypes(Type settingsType) =>
        App.Services
            .GetServices(typeof(IValidateOptions<>).MakeGenericType(settingsType))
            .OfType<Object>()
            .Select(validator => validator.GetType())
            .ToList();
}
