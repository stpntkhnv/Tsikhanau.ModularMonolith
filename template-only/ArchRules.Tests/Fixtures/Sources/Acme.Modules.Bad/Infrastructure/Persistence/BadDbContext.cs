using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Acme.Modules.Bad.Infrastructure.Persistence;

internal sealed class BadDbContext(DbContextOptions<BadDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "bad";
}
