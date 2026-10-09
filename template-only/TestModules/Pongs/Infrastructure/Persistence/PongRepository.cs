using Microsoft.EntityFrameworkCore;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure.Persistence;

internal sealed class PongRepository(PongsDbContext pongsDbContext) : IPongRepository
{
    public void Add(Pong pong) =>
        pongsDbContext
            .Set<Pong>()
            .Add(pong);

    public Task<Boolean> ExistsAsync(PongId pongId, CancellationToken cancellationToken) =>
        pongsDbContext
            .Set<Pong>()
            .AnyAsync(pong => pong.Id == pongId, cancellationToken);

    public Task TerminateConnectionAsync(CancellationToken cancellationToken) =>
        pongsDbContext.Database.ExecuteSqlRawAsync("SELECT pg_terminate_backend(pg_backend_pid())", cancellationToken);
}
