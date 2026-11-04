using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.Infrastructure.Modules;

internal static class ModuleServiceProviderExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public NpgsqlDataSource ResolveDataSource(ModuleDescriptor module) =>
            serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(module.Key);

        public ModuleDbContext ResolveDbContext(ModuleDescriptor module) =>
            (ModuleDbContext)serviceProvider.GetRequiredService(module.DbContextType);
    }
}
