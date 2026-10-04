using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.TestModules.Pongs.Contracts;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs;

internal interface IPongsModule : IPongsQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPongsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
