using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;

[InternalCommandName("touch-ping")]
internal sealed record TouchPingInternalCommand(Guid Id, Guid PingId) : InternalCommand(Id), IPingsCommand<Unit>;
