using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Persistence;

internal sealed class PingRepository(PingsDbContext pingsDbContext) : IPingRepository
{
    public void Add(Ping ping) =>
        pingsDbContext
            .Set<Ping>()
            .Add(ping);

    public async Task<Result<Ping>> GetAsync(PingId pingId, CancellationToken cancellationToken)
    {
        Optional<Ping> foundPing = await pingsDbContext
            .Set<Ping>()
            .FindAsync([pingId], cancellationToken);

        return foundPing.ToResult(PingErrors.NotFound);
    }
}
