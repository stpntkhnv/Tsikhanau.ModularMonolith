using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.TestModules.Pongs.Contracts;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs;

internal interface IPongsModule : IPongsQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPongsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
