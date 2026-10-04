using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongsDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<PongsDbContext>
{
    protected override String Schema => PongsDbContext.SchemaName;

    protected override PongsDbContext Create(DbContextOptions<PongsDbContext> dbContextOptions) =>
        new(dbContextOptions);
}
