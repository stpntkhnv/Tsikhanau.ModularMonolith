namespace Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

internal interface IPongRepository
{
    void Add(Pong pong);

    Task<Boolean> ExistsAsync(PongId pongId, CancellationToken cancellationToken);

    Task TerminateConnectionAsync(CancellationToken cancellationToken);
}
