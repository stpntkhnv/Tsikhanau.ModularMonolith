using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongsDbContext(DbContextOptions<PongsDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "pongs";

    protected override String Schema => SchemaName;

    protected override void ConfigureModule(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new PongConfiguration());
}
