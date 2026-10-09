using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Contracts;

public interface IPongsQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IPongsQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
