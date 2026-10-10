namespace Acme.Modules.Good.Contracts.Queries.GetWidget;

public sealed record GetWidgetQuery(Guid WidgetId) : IGoodQuery<WidgetDto>;
