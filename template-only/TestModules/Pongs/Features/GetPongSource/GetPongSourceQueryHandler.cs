using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Domain.Optionals;
using Tsikhanau.Monolith.TestModules.Pings.Contracts;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

internal sealed class GetPongSourceQueryHandler(
    ISqlConnectionFactory<IPongsModule> sqlConnectionFactory,
    IPingsQueryFacade pingsQueryFacade)
    : IQueryHandler<GetPongSourceQuery, PongSourceDto>
{
    private const String Sql = """
        SELECT ping_id
        FROM pongs.pongs
        WHERE id = @PongId
        """;

    public Task<Result<PongSourceDto>> Handle(GetPongSourceQuery query, CancellationToken cancellationToken) =>
        GetPingIdAsync(query.PongId, cancellationToken)
            .BindAsync(pingId => pingsQueryFacade.ExecuteQueryAsync(new GetPingQuery(pingId), cancellationToken))
            .MapAsync(ping => new PongSourceDto(query.PongId, ping.Label));

    private async Task<Result<Guid>> GetPingIdAsync(Guid pongId, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        var pingId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(Sql, new { PongId = pongId }, cancellationToken: cancellationToken));

        return pingId
            .AsOptional()
            .ToResult(PongErrors.NotFound);
    }
}
