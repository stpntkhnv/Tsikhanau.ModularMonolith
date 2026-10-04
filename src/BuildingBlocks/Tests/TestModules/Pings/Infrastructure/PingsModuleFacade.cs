using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;
using ModularMonolith.TestModules.Pings.Contracts;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Infrastructure;

internal sealed class PingsModuleFacade(IServiceScopeFactory serviceScopeFactory)
    : ModuleFacade<IPingsModule>(serviceScopeFactory), IPingsModule
{
    public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPingsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        SendAsync(command, cancellationToken);

    public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IPingsQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryAsync(query, cancellationToken);
}
