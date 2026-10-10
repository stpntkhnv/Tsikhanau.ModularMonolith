using Acme.Modules.Good.Contracts;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Railway;

namespace Acme.Modules.Good;

internal interface IGoodModule : IGoodQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IGoodCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
