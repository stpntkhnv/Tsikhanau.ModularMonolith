using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features;
using Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure;

internal static class PingsInfrastructureRegistration
{
    public static IServiceCollection AddPingsInfrastructure(this IServiceCollection services) =>
        services
            .AddModuleInfrastructure<IPingsModule, PingsModuleFacade, PingsDbContext>(ConstraintErrors)
            .AddScoped<IPingRepository, PingRepository>()
            .AddSingleton<PingTestHooks>();

    private static Dictionary<String, Error> ConstraintErrors =>
        new() { [PingConfiguration.LabelIndex] = PingErrors.LabelTaken };
}
