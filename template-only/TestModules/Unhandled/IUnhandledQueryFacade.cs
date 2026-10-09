using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Unhandled;

internal interface IUnhandledQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IUnhandledQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
