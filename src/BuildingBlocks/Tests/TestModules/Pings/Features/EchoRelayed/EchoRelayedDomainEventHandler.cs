using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using ModularMonolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.EchoRelayed;

internal sealed class EchoRelayedDomainEventHandler(IPingRepository pingRepository)
    : IDomainEventHandler<EchoRelayedDomainEvent>
{
    public Task<Result<Unit>> Handle(EchoRelayedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(domainEvent.TargetPingId, cancellationToken)
            .BindAsync(targetPing => targetPing.AddEcho(domainEvent.EchoId, domainEvent.Text));
}
