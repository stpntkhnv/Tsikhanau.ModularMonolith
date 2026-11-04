using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.AuditCrates;

internal sealed class AuditCratesQueryHandler(IServiceProvider serviceProvider)
    : IQueryHandler<AuditCratesQuery, Int32>
{
    public async Task<Result<Int32>> Handle(AuditCratesQuery query, CancellationToken cancellationToken)
    {
        var outbox = (IOutbox<IBadModule>)serviceProvider.GetService(typeof(IOutbox<IBadModule>))!;
        await Task.Yield();
        return outbox.GetHashCode();
    }
}
