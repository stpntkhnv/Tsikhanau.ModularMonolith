using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

internal static class ModuleServiceProviderExtensions
{
    public static NpgsqlDataSource ResolveDataSource(this IServiceProvider serviceProvider, ModuleDescriptor module) =>
        serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key);

    public static ModuleDbContext ResolveDbContext(this IServiceProvider serviceProvider, ModuleDescriptor module) =>
        (ModuleDbContext)serviceProvider.GetRequiredService(module.DbContextType);
}
