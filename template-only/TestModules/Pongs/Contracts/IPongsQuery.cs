using Tsikhanau.Monolith.Contracts.Queries;

namespace Tsikhanau.Monolith.TestModules.Pongs.Contracts;

public interface IPongsQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
