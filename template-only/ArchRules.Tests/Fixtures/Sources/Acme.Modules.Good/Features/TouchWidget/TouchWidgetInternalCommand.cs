using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.TouchWidget;

[InternalCommandName("touch-widget")]
internal sealed record TouchWidgetInternalCommand(Guid Id, Guid WidgetId) : InternalCommand(Id), IGoodCommand<Unit>;
