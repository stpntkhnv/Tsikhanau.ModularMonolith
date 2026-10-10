namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed record CreateWidgetCommand(Guid WidgetId, String Label) : IGoodCommand<Guid>;
