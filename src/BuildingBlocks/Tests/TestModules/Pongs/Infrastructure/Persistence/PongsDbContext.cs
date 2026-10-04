using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongsDbContext(DbContextOptions<PongsDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "pongs";

    protected override String Schema => SchemaName;

    protected override void ConfigureModule(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new PongConfiguration());
}
