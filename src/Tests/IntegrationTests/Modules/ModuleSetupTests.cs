using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Infrastructure.Inbox;
using Tsikhanau.Monolith.Infrastructure.InternalCommands;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Outbox;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.IntegrationTests.Modules;

public sealed class ModuleSetupTests(AppFixture appFixture) : AppTest(appFixture)
{
    private static readonly String[] QueueTables =
        [InboxMessage.Table, InternalCommandMessage.Table, OutboxMessage.Table];

    private static readonly String[] CommonTables = [ModuleDbContextOptions.MigrationsHistoryTable, .. QueueTables];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_module_folder_has_exactly_one_registered_module() =>
        RegisteredModuleNames.ShouldBe(ProductionModules.Names, ignoreOrder: true);

    [Fact]
    public void Every_module_class_has_the_name_of_the_module_it_registers()
    {
        var moduleClassNames = App.Services
            .GetServices<IModule>()
            .Select(module => module.Name)
            .ToList();

        moduleClassNames.ShouldBe(RegisteredModuleNames, ignoreOrder: true);
    }

    [Theory]
    [EveryProductionModule]
    public void Module_facade_is_registered(String name)
    {
        var moduleFacade = App.Services.GetService(App.GetModule(name).Key);

        moduleFacade.ShouldNotBeNull();
    }

    [Theory]
    [EveryProductionModule]
    public void Query_facade_is_the_module_facade(String name)
    {
        var module = App.GetModule(name);

        Assert.SkipWhen(module.QueryFacadeKey.IsNone, $"{name} has no query facade");

        var moduleFacade = App.Services.GetRequiredService(module.Key);
        var queryFacade = App.Services.GetRequiredService(module.QueryFacadeKey.Value);

        queryFacade.ShouldBeSameAs(moduleFacade);
    }

    [Theory]
    [EveryProductionModule]
    public void Module_settings_are_validated_with_data_annotations(String name)
    {
        var settingsTypes = GetSettingsTypes(App.GetModule(name));

        Assert.SkipWhen(settingsTypes.Count == 0, $"{name} has no settings");

        var settingsTypesWithoutValidation = settingsTypes
            .Where(settingsType => !IsValidatedWithDataAnnotations(settingsType))
            .ToList();

        settingsTypesWithoutValidation.ShouldBeEmpty();
    }

    [Theory]
    [EveryProductionModule]
    public async Task Module_schema_holds_exactly_the_queues_and_its_own_tables(String name)
    {
        var module = App.GetModule(name);
        var tableNames = await App.Database.ReadTableNamesAsync(module.Schema);

        String[] expectedTableNames = [.. CommonTables, .. GetOwnTableNames(module)];

        tableNames.ShouldBe(expectedTableNames, ignoreOrder: true);
    }

    [Theory]
    [EveryProductionModule]
    public async Task Module_database_health_check_is_healthy(String name)
    {
        var healthCheckName = $"database-{App.GetModule(name).Schema}";
        var healthCheckService = App.Services.GetRequiredService<HealthCheckService>();

        var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken);

        healthReport.Entries.ShouldContainKey(healthCheckName);
        healthReport.Entries[healthCheckName].Status.ShouldBe(HealthStatus.Healthy);
    }

    private List<String> RegisteredModuleNames =>
        App.Services
            .GetRequiredService<ModuleRegistry>().Modules
            .Select(module => module.Name)
            .ToList();

    private static List<Type> GetSettingsTypes(ModuleDescriptor module) =>
        module.Key.Assembly
            .GetTypes()
            .Where(type => type.FullName == $"{module.Key.Namespace}.Settings.{module.Name}Settings")
            .ToList();

    private Boolean IsValidatedWithDataAnnotations(Type settingsType) =>
        GetValidatorTypes(settingsType)
            .Contains(typeof(DataAnnotationValidateOptions<>).MakeGenericType(settingsType));

    private List<Type> GetValidatorTypes(Type settingsType) =>
        App.Services
            .GetServices(typeof(IValidateOptions<>).MakeGenericType(settingsType))
            .OfType<Object>()
            .Select(validator => validator.GetType())
            .ToList();

    private List<String> GetOwnTableNames(ModuleDescriptor module)
    {
        using var scope = App.Services.CreateScope();
        var dbContext = ResolveDbContext(scope.ServiceProvider, module);

        return dbContext.Model
            .GetRelationalModel()
            .Tables
            .Where(table => table.Schema == module.Schema)
            .Select(table => table.Name)
            .Except(QueueTables)
            .ToList();
    }

    private static DbContext ResolveDbContext(IServiceProvider serviceProvider, ModuleDescriptor module) =>
        (DbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
