using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.CreateWidget;

internal sealed record CreateWidgetRequest(String Label, Decimal Price, Optional<Guid> WidgetId);
