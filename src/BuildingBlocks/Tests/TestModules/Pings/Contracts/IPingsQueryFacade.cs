using ModularMonolith.BuildingBlocks.Contracts.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Contracts;

public interface IPingsQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IPingsQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
