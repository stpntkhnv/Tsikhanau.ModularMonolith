using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Contracts;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure;

internal static class PingsInfrastructureRegistration
{
    public static IServiceCollection AddPingsInfrastructure(this IServiceCollection services) =>
        services
            .AddModuleInfrastructure<IPingsModule, IPingsQueryFacade, PingsModuleFacade, PingsDbContext>(Definition)
            .AddScoped<IPingRepository, PingRepository>()
            .AddSingleton<PingTestHooks>();

    private static ModuleDefinition Definition =>
        new("Pings", PingsDbContext.SchemaName, typeof(IPingsModule).Assembly, "Tsikhanau.Monolith.TestModules.Pings")
        {
            ConstraintErrors = new Dictionary<String, Error>
            {
                [PingConfiguration.LabelIndex] = PingErrors.LabelTaken,
            },
        };
}
