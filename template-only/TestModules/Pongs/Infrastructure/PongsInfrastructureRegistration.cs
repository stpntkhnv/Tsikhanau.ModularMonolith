using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Monolith.TestModules.Pongs.Features;
using Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;

internal static class PongsInfrastructureRegistration
{
    public static IServiceCollection AddPongsInfrastructure(this IServiceCollection services) =>
        services
            .AddModuleInfrastructure<IPongsModule, PongsModuleFacade, PongsDbContext>()
            .AddScoped<IPongRepository, PongRepository>()
            .AddSingleton<PongTestHooks>();
}
