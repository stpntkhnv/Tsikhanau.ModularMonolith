namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed record CreateWidgetCommand(Guid WidgetId, String Label, Decimal Price) : IGoodCommand<Guid>;
