using Acme.Modules.Good.Contracts.IntegrationEvents;
using Acme.Modules.Good.Domain.Widgets.Events;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.WidgetCreated;

internal sealed class WidgetCreatedDomainEventHandler(IOutbox<IGoodModule> outbox)
    : IDomainEventHandler<WidgetCreatedDomainEvent>
{
    public Task<Result<Unit>> Handle(WidgetCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Add(new WidgetCreatedIntegrationEvent(domainEvent.OccurredOn, domainEvent.WidgetId));
        return Task.FromResult<Result<Unit>>(Unit.Value);
    }
}
