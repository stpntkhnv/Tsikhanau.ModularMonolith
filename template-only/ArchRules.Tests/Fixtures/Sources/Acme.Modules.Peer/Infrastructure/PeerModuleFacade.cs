using Acme.Modules.Peer.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Railway;

namespace Acme.Modules.Peer.Infrastructure;

internal sealed class PeerModuleFacade(IServiceScopeFactory serviceScopeFactory)
    : ModuleFacade<IPeerModule>(serviceScopeFactory), IPeerModule
{
    public Task<Result<TResult>> ExecuteQueryAsync<TResult>(
        IPeerQuery<TResult> query,
        CancellationToken cancellationToken)
        where TResult : notnull =>
        QueryAsync(query, cancellationToken);
}
