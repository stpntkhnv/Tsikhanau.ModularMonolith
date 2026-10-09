using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.TestModules.Pings.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.PingCreated;

internal sealed class PingCreatedDomainEventHandler(IOutbox<IPingsModule> outbox)
    : IDomainEventHandler<PingCreatedDomainEvent>
{
    public Task<Result<Unit>> Handle(PingCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(
            new PingCreatedIntegrationEvent(
                domainEvent.OccurredOn,
                domainEvent.PingId.Value,
                domainEvent.Label.Value));

        return Task.FromResult(Result.Success());
    }
}
