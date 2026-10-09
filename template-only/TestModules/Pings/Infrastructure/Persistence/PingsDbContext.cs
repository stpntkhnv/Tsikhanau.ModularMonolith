using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingsDbContext(DbContextOptions<PingsDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "pings";

    protected override String Schema => SchemaName;

    protected override void ConfigureModule(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new PingConfiguration());
}
