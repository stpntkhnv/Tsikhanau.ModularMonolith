using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.ScheduleTouch;

internal sealed record ScheduleTouchCommand(Guid PingId) : IPingsCommand<Unit>;
