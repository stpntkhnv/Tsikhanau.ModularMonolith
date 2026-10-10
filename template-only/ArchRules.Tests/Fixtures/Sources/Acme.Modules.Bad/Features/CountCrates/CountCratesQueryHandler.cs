using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.CountCrates;

internal sealed class CountCratesQueryHandler(IOutbox<IBadModule> outbox) : IQueryHandler<CountCratesQuery, Int32>
{
    public Task<Result<Int32>> Handle(CountCratesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Int32>>(outbox.GetHashCode());
}
