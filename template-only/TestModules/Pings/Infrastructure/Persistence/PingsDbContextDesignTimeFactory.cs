using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingsDbContextDesignTimeFactory : ModuleDbContextDesignTimeFactory<PingsDbContext>
{
    protected override String Schema => PingsDbContext.SchemaName;

    protected override PingsDbContext Create(DbContextOptions<PingsDbContext> dbContextOptions) =>
        new(dbContextOptions);
}
