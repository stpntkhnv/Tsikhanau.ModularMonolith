using Acme.Modules.Peer.Contracts.Queries.GetPeerName;
using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Peer.Features.GetPeerName;

internal sealed class GetPeerNameQueryHandler(ISqlConnectionFactory<IPeerModule> sqlConnectionFactory)
    : IQueryHandler<GetPeerNameQuery, String>
{
    private const String Sql = "SELECT name FROM peer.peers WHERE id = @PeerId";

    public async Task<Result<String>> Handle(GetPeerNameQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<String>(
            new CommandDefinition(Sql, new { query.PeerId }, cancellationToken: cancellationToken));
    }
}
