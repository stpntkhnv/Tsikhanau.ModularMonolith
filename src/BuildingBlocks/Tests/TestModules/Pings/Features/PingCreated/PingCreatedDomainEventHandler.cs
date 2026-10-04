using ModularMonolith.BuildingBlocks.Application.Events;
using ModularMonolith.TestModules.Pings.Contracts.IntegrationEvents;
using ModularMonolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.PingCreated;

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
