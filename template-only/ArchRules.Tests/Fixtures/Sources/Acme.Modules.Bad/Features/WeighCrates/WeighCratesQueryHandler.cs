using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.WeighCrates;

internal sealed class WeighCratesQueryHandler(IServiceProvider serviceProvider)
    : IQueryHandler<WeighCratesQuery, Int32>
{
    public Task<Result<Int32>> Handle(WeighCratesQuery query, CancellationToken cancellationToken)
    {
        var outbox = (IOutbox<IBadModule>)serviceProvider.GetService(typeof(IOutbox<IBadModule>))!;
        return Task.FromResult<Result<Int32>>(outbox.GetHashCode() + outbox.GetHashCode());
    }
}
