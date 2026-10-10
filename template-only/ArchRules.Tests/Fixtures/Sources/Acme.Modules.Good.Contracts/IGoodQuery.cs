using Tsikhanau.Monolith.Contracts.Queries;

namespace Acme.Modules.Good.Contracts;

public interface IGoodQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
