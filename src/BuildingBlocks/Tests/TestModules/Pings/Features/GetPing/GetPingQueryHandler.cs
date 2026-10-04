using Dapper;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.GetPing;

internal sealed class GetPingQueryHandler(ISqlConnectionFactory<IPingsModule> sqlConnectionFactory)
    : IQueryHandler<GetPingQuery, PingDto>
{
    private const String Sql = """
        SELECT id AS "Id", label AS "Label", touches AS "Touches", version AS "Version"
        FROM pings.pings
        WHERE id = @PingId
        """;

    public async Task<Result<PingDto>> Handle(GetPingQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        Optional<PingDto> foundPing = await connection.QuerySingleOrDefaultAsync<PingDto>(
            new CommandDefinition(Sql, new { query.PingId }, cancellationToken: cancellationToken));

        return foundPing.ToResult(PingErrors.NotFound);
    }
}
