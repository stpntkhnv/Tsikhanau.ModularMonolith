using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using ModularMonolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.PingLooped;

internal sealed class PingLoopedDomainEventHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : IDomainEventHandler<PingLoopedDomainEvent>
{
    public Task<Result<Unit>> Handle(PingLoopedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(domainEvent.PingId, cancellationToken)
            .BindAsync(ping => ping.Loop(domainEvent.RemainingRounds, timeProvider.GetUtcNow()));
}
