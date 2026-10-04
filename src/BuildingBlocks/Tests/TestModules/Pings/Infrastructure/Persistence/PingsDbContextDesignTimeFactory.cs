using Microsoft.EntityFrameworkCore;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

namespace ModularMonolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingsDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<PingsDbContext>
{
    protected override String Schema => PingsDbContext.SchemaName;

    protected override PingsDbContext Create(DbContextOptions<PingsDbContext> dbContextOptions) =>
        new(dbContextOptions);
}
