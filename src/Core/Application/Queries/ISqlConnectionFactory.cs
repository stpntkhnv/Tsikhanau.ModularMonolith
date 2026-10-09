using System.Data.Common;
using Tsikhanau.Monolith.Application.Modules;

namespace Tsikhanau.Monolith.Application.Queries;

public interface ISqlConnectionFactory<TModule>
    where TModule : IModuleFacade
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
