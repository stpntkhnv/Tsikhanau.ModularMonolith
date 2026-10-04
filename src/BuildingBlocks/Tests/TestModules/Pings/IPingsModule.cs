using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.TestModules.Pings.Contracts;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings;

internal interface IPingsModule : IPingsQueryFacade, IModuleFacade
{
    Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPingsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull;
}
