using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;
using ModularMonolith.TestModules.Pongs.Contracts;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Infrastructure;

internal sealed class PongsModuleFacade(IServiceScopeFactory serviceScopeFactory)
    : ModuleFacade<IPongsModule>(serviceScopeFactory), IPongsModule
{
    public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IPongsCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        SendAsync(command, cancellationToken);

    public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IPongsQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryAsync(query, cancellationToken);
}
