using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.TestModules.Pings.Contracts;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings;

internal interface IPingsModule : IPingsQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPingsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
