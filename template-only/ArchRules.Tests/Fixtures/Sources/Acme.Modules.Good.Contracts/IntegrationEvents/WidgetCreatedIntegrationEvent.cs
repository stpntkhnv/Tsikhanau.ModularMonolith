using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Acme.Modules.Good.Contracts.IntegrationEvents;

[IntegrationEventName("good.widget-created")]
public sealed record WidgetCreatedIntegrationEvent(DateTimeOffset OccurredOn, Guid WidgetId)
    : IntegrationEvent(OccurredOn);
