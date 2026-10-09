using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.MailToken;

internal sealed class ScheduleTokenMailCommandHandler(
    IInternalCommandScheduler<IPingsModule> internalCommandScheduler,
    TimeProvider timeProvider)
    : ICommandHandler<ScheduleTokenMailCommand, Unit>
{
    public Task<Result<Unit>> Handle(ScheduleTokenMailCommand command, CancellationToken cancellationToken)
    {
        internalCommandScheduler.Enqueue(
            new MailTokenInternalCommand(
                Guid.CreateVersion7(timeProvider.GetUtcNow()),
                command.PingId,
                command.Token));

        return Task.FromResult(Result.Success());
    }
}
