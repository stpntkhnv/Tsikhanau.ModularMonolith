using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;
using ModularMonolith.TestModules.Pongs.Contracts;
using ModularMonolith.TestModules.Pongs.Domain.Pongs;
using ModularMonolith.TestModules.Pongs.Features;
using ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

namespace ModularMonolith.TestModules.Pongs.Infrastructure;

internal static class PongsInfrastructureRegistration
{
    public static IServiceCollection AddPongsInfrastructure(this IServiceCollection services) =>
        services
            .AddModuleInfrastructure<IPongsModule, IPongsQueryFacade, PongsModuleFacade, PongsDbContext>(Definition)
            .AddScoped<IPongRepository, PongRepository>()
            .AddSingleton<PongTestHooks>();

    private static ModuleDefinition Definition =>
        new("Pongs", PongsDbContext.SchemaName, typeof(IPongsModule).Assembly, "ModularMonolith.TestModules.Pongs");
}
