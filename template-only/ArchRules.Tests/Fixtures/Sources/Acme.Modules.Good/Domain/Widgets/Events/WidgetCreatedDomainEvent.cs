using Tsikhanau.Monolith.Domain.Events;

namespace Acme.Modules.Good.Domain.Widgets.Events;

internal sealed record WidgetCreatedDomainEvent(DateTimeOffset OccurredOn, Guid WidgetId) : DomainEvent(OccurredOn);
