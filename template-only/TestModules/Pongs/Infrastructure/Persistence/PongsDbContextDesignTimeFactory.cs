using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongsDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<PongsDbContext>
{
    protected override String Schema => PongsDbContext.SchemaName;

    protected override PongsDbContext Create(DbContextOptions<PongsDbContext> dbContextOptions) =>
        new(dbContextOptions);
}
