using Tsikhanau.Monolith.Contracts.Queries;

namespace Tsikhanau.Monolith.TestModules.Pings.Contracts;

public interface IPingsQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
