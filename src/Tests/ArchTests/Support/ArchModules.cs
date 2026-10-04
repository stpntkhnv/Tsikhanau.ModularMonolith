using System.Reflection;
using ModularMonolith.BuildingBlocks.Api.Endpoints;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using ModularMonolith.Testing.Modules;

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

    public static Assembly TestModulesAssembly => Assembly.Load("ModularMonolith.TestModules");

    public static ArchModule Get(String name) =>
        Production.Contains(name)
            ? new ArchModule(name, $"ModularMonolith.Modules.{name}", LoadProductionAssemblies(name))
            : new ArchModule(name, $"ModularMonolith.TestModules.{name}", [TestModulesAssembly]);

    public static Assembly LoadModuleAssembly(String name) => Assembly.Load($"ModularMonolith.Modules.{name}");

    public static Assembly LoadContractsAssembly(String name) =>
        Assembly.Load($"ModularMonolith.Modules.{name}.Contracts");

    private static List<Assembly> LoadProductionAssemblies(String name) =>
        [LoadModuleAssembly(name), LoadContractsAssembly(name)];
}
