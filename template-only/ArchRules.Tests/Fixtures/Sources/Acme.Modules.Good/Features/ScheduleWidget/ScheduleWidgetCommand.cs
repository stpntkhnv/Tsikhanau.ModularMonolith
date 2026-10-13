using Tsikhanau.Railway;

namespace Acme.Modules.Good.Features.ScheduleWidget;

internal sealed record ScheduleWidgetCommand(Guid WidgetId) : IGoodCommand<Unit>;
