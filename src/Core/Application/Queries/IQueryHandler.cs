using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Queries;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);
}
