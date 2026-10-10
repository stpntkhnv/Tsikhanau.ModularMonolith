using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Contracts;

public interface IGoodQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IGoodQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
