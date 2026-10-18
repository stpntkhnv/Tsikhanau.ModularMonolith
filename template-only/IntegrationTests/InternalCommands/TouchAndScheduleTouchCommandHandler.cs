using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.IntegrationTests.InternalCommands;

internal sealed class TouchAndScheduleTouchCommandHandler(
    IPingRepository pingRepository,
    IInternalCommandScheduler<IPingsModule> internalCommandScheduler,
    TimeProvider timeProvider)
    : ICommandHandler<TouchAndScheduleTouchCommand, Unit>
{
    public Task<Result<Unit>> Handle(TouchAndScheduleTouchCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()))
            .TapAsync(_ => ScheduleTouch(command.PingId));

    private void ScheduleTouch(Guid pingId) =>
        internalCommandScheduler.Enqueue(
            new TouchPingInternalCommand(Guid.CreateVersion7(timeProvider.GetUtcNow()), pingId));
}
