using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.ScheduleTouch;

internal sealed class TouchPingInternalCommandHandler(
    IPingRepository pingRepository,
    PingTestHooks pingTestHooks,
    TimeProvider timeProvider)
    : ICommandHandler<TouchPingInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(TouchPingInternalCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()));
}
