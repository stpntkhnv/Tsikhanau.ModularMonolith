using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.PingLooped;

internal sealed class PingLoopedDomainEventHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : IDomainEventHandler<PingLoopedDomainEvent>
{
    public Task<Result<Unit>> Handle(PingLoopedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(domainEvent.PingId, cancellationToken)
            .BindAsync(ping => ping.Loop(domainEvent.RemainingRounds, timeProvider.GetUtcNow()));
}
