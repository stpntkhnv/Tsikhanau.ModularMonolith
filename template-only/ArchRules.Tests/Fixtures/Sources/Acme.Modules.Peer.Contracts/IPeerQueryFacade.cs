using Tsikhanau.Monolith.Contracts.Modules;
using Tsikhanau.Railway;

namespace Acme.Modules.Peer.Contracts;

public interface IPeerQueryFacade : IModuleQueryFacade
{
    Task<Result<TResult>> ExecuteQueryAsync<TResult>(IPeerQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull;
}
