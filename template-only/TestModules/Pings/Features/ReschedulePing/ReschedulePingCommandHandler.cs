using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ReschedulePing;

internal sealed class ReschedulePingCommandHandler(IPingRepository pingRepository)
    : ICommandHandler<ReschedulePingCommand, Unit>
{
    public Task<Result<Unit>> Handle(ReschedulePingCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => Reschedule(ping, command));

    private static Result<Unit> Reschedule(Ping ping, ReschedulePingCommand command) =>
        PingSchedule
            .Create(command.Interval, command.Repeats, "schedule")
            .Bind(ping.Reschedule);
}
