using Acme.Modules.Bad.Contracts;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad;

internal interface IBadModule : IBadQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IBadCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
