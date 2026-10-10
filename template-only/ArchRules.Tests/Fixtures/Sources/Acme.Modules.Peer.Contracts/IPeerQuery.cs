using Tsikhanau.Monolith.Contracts.Queries;

namespace Acme.Modules.Peer.Contracts;

public interface IPeerQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
