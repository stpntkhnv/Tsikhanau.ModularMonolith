using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.CountPongs;

internal sealed class CountPongsQueryHandler(ISqlConnectionFactory<IPongsModule> sqlConnectionFactory)
    : IQueryHandler<CountPongsQuery, Int32>
{
    public async Task<Result<Int32>> Handle(CountPongsQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Int32>(
            new CommandDefinition("SELECT count(*)::int FROM pongs.pongs", cancellationToken: cancellationToken));
    }
}
