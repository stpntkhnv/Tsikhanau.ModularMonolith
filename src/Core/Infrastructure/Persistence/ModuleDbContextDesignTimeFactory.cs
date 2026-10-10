using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public abstract class ModuleDbContextDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : ModuleDbContext
{
    private const String PlaceholderConnectionString = "Host=localhost;Database=placeholder;Username=placeholder";

    private static String Schema => ModuleSchema.Get(typeof(TDbContext));

    public TDbContext CreateDbContext(String[] args)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, PlaceholderConnectionString, Schema);
        return typeof(TDbContext).CreateInstance<TDbContext>(dbContextOptionsBuilder.Options);
    }
}
