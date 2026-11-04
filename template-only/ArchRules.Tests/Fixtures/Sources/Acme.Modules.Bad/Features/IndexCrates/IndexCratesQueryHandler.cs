using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.IndexCrates;

internal sealed class IndexCratesQueryHandler : IQueryHandler<IndexCratesQuery, Int32>
{
    public Int32 this[IOutbox<IBadModule> outbox] => outbox.GetHashCode();

    public Task<Result<Int32>> Handle(IndexCratesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Int32>>(0);
}
