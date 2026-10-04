using ModularMonolith.BuildingBlocks.Contracts.Queries;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Application.Queries;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);
}
