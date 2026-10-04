using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ModularMonolith.BuildingBlocks.Infrastructure.Database;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public abstract class ModuleDbContextDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : ModuleDbContext
{
    protected abstract String Schema { get; }

    private String DesignTimeConnectionString =>
        $"Host=localhost;Database=modularmonolith;Username={ModuleLogin.BuildName(Schema)}";

    public TDbContext CreateDbContext(String[] args)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ModuleDbContextOptions.Configure(dbContextOptionsBuilder, DesignTimeConnectionString, Schema);
        return Create(dbContextOptionsBuilder.Options);
    }

    protected abstract TDbContext Create(DbContextOptions<TDbContext> dbContextOptions);
}
