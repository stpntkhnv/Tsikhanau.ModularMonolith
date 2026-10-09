using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.PingTouched;

internal sealed class PingTouchedDomainEventHandler(IOutbox<IPingsModule> outbox)
    : IDomainEventHandler<PingTouchedDomainEvent>
{
    public Task<Result<Unit>> Handle(PingTouchedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            new PingTouchedIntegrationEvent(
                domainEvent.OccurredOn,
                domainEvent.PingId.Value,
                domainEvent.Touches));

        return Task.FromResult(Result.Success());
    }
}
