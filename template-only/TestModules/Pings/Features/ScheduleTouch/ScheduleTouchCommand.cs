using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;

internal sealed record ScheduleTouchCommand(Guid PingId) : IPingsCommand<Unit>;
