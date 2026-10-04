using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal interface IPingRepository
{
    void Add(Ping ping);

    Task<Result<Ping>> GetAsync(PingId pingId, CancellationToken cancellationToken);
}
