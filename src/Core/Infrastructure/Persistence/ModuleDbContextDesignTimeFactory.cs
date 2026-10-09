using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public abstract class ModuleDbContextDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : ModuleDbContext
{
    private const String PlaceholderConnectionString = "Host=localhost;Database=placeholder;Username=placeholder";

    protected abstract String Schema { get; }

    public TDbContext CreateDbContext(String[] args)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, PlaceholderConnectionString, Schema);
        return Create(dbContextOptionsBuilder.Options);
    }

    protected abstract TDbContext Create(DbContextOptions<TDbContext> dbContextOptions);
}
