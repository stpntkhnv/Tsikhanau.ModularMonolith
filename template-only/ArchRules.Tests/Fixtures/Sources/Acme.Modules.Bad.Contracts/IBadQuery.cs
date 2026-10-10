using Tsikhanau.Monolith.Contracts.Queries;

namespace Acme.Modules.Bad.Contracts;

public interface IBadQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
