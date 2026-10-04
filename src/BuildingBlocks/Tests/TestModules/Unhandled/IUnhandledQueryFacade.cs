using ModularMonolith.BuildingBlocks.Contracts.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Unhandled;

internal interface IUnhandledQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IUnhandledQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
