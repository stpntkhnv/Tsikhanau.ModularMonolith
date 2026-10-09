using System.Reflection;
using Tsikhanau.Monolith.Api.Endpoints;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Support;

public static class ArchModules
{
    public static readonly String[] Production = ProductionModules.Names;

    public static readonly String[] TestOnly = ["Pings", "Pongs"];

    public static IEnumerable<String> Names => Production.Concat(TestOnly);

    public static TheoryData<String> All => [.. Names];

    public static IReadOnlyList<Assembly> BuildingBlockAssemblies =>
    [
        typeof(AggregateRoot<>).Assembly,
        typeof(IntegrationEvent).Assembly,
        typeof(IModuleFacade).Assembly,
        typeof(ModuleDbContext).Assembly,
        typeof(IEndpoint).Assembly,
    ];

    public static IReadOnlyList<Assembly> ModuleAssemblies =>
        Production
            .SelectMany(name => Get(name).Assemblies)
            .Append(TestModulesAssembly)
            .Distinct()
            .ToList();

    public static IReadOnlyList<Type> AllModuleTypes =>
        Names
            .SelectMany(name => Get(name).Types)
            .ToList();

    public static Assembly TestModulesAssembly => Assembly.Load("Tsikhanau.Monolith.TestModules");

    public static ArchModule Get(String name) =>
        Production.Contains(name)
            ? new ArchModule(name, $"ModularMonolith.Modules.{name}", LoadProductionAssemblies(name))
            : new ArchModule(name, $"Tsikhanau.Monolith.TestModules.{name}", [TestModulesAssembly]);

    public static Assembly LoadModuleAssembly(String name) => Assembly.Load($"ModularMonolith.Modules.{name}");

    public static Assembly LoadContractsAssembly(String name) =>
        Assembly.Load($"ModularMonolith.Modules.{name}.Contracts");

    private static List<Assembly> LoadProductionAssemblies(String name) =>
        [LoadModuleAssembly(name), LoadContractsAssembly(name)];
}
