using Dapper;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Domain.Optionals;
using ModularMonolith.TestModules.Pings.Contracts;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using ModularMonolith.TestModules.Pongs.Domain.Pongs;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Features.GetPongSource;

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
