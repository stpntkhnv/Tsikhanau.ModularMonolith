using Dapper;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.TouchCrates;

internal sealed class TouchCratesQueryHandler(ISqlConnectionFactory<IBadModule> sqlConnectionFactory)
    : IQueryHandler<TouchCratesQuery, Int32>
{
    public async Task<Result<Int32>> Handle(TouchCratesQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await sqlConnectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition("UPDATE bad.crates SET touched = now()", cancellationToken: cancellationToken));
    }
}
