using System.Data.Common;
using Npgsql;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

public sealed class SqlConnectionFactory<TModule>(NpgsqlDataSource npgsqlDataSource) : ISqlConnectionFactory<TModule>
    where TModule : IModuleFacade
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
}
