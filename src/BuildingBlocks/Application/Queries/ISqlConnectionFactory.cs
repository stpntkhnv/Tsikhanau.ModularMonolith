using System.Data.Common;
using ModularMonolith.BuildingBlocks.Application.Modules;

namespace ModularMonolith.BuildingBlocks.Application.Queries;

public interface ISqlConnectionFactory<TModule>
    where TModule : IModuleFacade
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
