using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.TestModules.Pings.Contracts;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using ModularMonolith.TestModules.Pings.Features;
using ModularMonolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Infrastructure;

internal static class PingsInfrastructureRegistration
{
    public static IServiceCollection AddPingsInfrastructure(this IServiceCollection services) =>
        services
            .AddModuleInfrastructure<IPingsModule, IPingsQueryFacade, PingsModuleFacade, PingsDbContext>(Definition)
            .AddScoped<IPingRepository, PingRepository>()
            .AddSingleton<PingTestHooks>();

    private static ModuleDefinition Definition =>
        new("Pings", PingsDbContext.SchemaName, typeof(IPingsModule).Assembly, "ModularMonolith.TestModules.Pings")
        {
            ConstraintErrors = new Dictionary<String, Error>
            {
                [PingConfiguration.LabelIndex] = PingErrors.LabelTaken,
            },
        };
}
