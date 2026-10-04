using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingsDbContext(DbContextOptions<PingsDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "pings";

    protected override String Schema => SchemaName;

    protected override void ConfigureModule(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new PingConfiguration());
}
