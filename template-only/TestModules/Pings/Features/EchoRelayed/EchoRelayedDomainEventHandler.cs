using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.EchoRelayed;

internal sealed class EchoRelayedDomainEventHandler(IPingRepository pingRepository)
    : IDomainEventHandler<EchoRelayedDomainEvent>
{
    public Task<Result<Unit>> Handle(EchoRelayedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(domainEvent.TargetPingId, cancellationToken)
            .BindAsync(targetPing => targetPing.AddEcho(domainEvent.EchoId, domainEvent.Text));
}
