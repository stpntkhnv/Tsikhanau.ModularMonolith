using System.Data.Common;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Application.Queries;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Persistence;

public sealed class SqlConnectionFactory<TModule>(NpgsqlDataSource npgsqlDataSource) : ISqlConnectionFactory<TModule>
    where TModule : IModuleFacade
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
}
