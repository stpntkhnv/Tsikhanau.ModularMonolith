using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.ScheduleTouch;

[InternalCommandName("touch-ping")]
internal sealed record TouchPingInternalCommand(Guid Id, Guid PingId) : InternalCommand(Id), IPingsCommand<Unit>;
