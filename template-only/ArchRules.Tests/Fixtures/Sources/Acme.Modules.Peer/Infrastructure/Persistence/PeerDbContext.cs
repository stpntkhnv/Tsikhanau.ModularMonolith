using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.Infrastructure.Persistence;

namespace Acme.Modules.Peer.Infrastructure.Persistence;

internal sealed class PeerDbContext(DbContextOptions<PeerDbContext> dbContextOptions)
    : ModuleDbContext(dbContextOptions)
{
    public const String SchemaName = "peer";

    protected override String Schema => SchemaName;
}
