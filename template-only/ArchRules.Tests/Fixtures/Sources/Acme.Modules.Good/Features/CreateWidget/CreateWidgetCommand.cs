using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed record CreateWidgetCommand(Optional<Guid> WidgetId, String Label, Decimal Price) : IGoodCommand<Guid>;
