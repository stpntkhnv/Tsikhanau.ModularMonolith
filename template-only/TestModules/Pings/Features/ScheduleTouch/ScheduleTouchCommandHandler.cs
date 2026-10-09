using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ScheduleTouch;

internal sealed class ScheduleTouchCommandHandler(
    IInternalCommandScheduler<IPingsModule> internalCommandScheduler,
    TimeProvider timeProvider)
    : ICommandHandler<ScheduleTouchCommand, Unit>
{
    public Task<Result<Unit>> Handle(ScheduleTouchCommand command, CancellationToken cancellationToken)
    {
        internalCommandScheduler.Enqueue(
            new TouchPingInternalCommand(Guid.CreateVersion7(timeProvider.GetUtcNow()), command.PingId));

        return Task.FromResult(Result.Success());
    }
}
