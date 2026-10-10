using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Contracts;

public interface IBadQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IBadQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
