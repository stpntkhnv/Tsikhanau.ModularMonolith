using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.TestModules.Pongs.Contracts;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Infrastructure;

internal sealed class PongsModuleFacade(IServiceScopeFactory serviceScopeFactory)
    : ModuleFacade<IPongsModule>(serviceScopeFactory), IPongsModule
{
    public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IPongsQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryAsync(query, cancellationToken);
}
