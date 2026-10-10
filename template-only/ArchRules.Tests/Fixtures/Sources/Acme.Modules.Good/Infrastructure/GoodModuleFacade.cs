using Acme.Modules.Good.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Infrastructure;

internal sealed class GoodModuleFacade(IServiceScopeFactory serviceScopeFactory)
    : ModuleFacade<IGoodModule>(serviceScopeFactory), IGoodModule
{
    public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
        IGoodCommand<TResult> command,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        SendAsync(command, cancellationToken);

    public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IGoodQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryAsync(query, cancellationToken);
}
