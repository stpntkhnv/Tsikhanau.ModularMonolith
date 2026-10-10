using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.GetPing;

internal sealed class GetPingQueryHandler(ISqlConnectionFactory<IPingsModule> sqlConnectionFactory)
    : IQueryHandler<GetPingQuery, PingDto>
{
    private const String Sql = """
        SELECT id AS "Id", label AS "Label", nickname AS "Nickname", touches AS "Touches", version AS "Version"
        FROM pings.pings
        WHERE id = @PingId
        """;

    public async Task<Result<PingDto>> Handle(GetPingQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        Optional<PingRow> foundPingRow = await connection.QuerySingleOrDefaultAsync<PingRow>(
            new CommandDefinition(Sql, new { query.PingId }, cancellationToken: cancellationToken));

        return foundPingRow
            .Map(pingRow => pingRow.ToDto())
            .ToResult(PingErrors.NotFound);
    }
}
