using ModularMonolith.BuildingBlocks.Contracts.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Contracts;

public interface IPongsQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IPongsQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
