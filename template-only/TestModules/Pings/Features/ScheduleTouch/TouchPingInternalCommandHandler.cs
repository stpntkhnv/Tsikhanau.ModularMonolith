using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;

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
