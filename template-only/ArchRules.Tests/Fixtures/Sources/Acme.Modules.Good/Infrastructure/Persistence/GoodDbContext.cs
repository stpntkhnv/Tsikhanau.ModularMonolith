using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Acme.Modules.Good.Infrastructure.Persistence;

internal sealed class GoodDbContext(DbContextOptions<GoodDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "good";

    protected override String Schema => SchemaName;
}
