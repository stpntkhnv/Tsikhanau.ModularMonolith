using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.LoopPing;

internal sealed class LoopPingCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<LoopPingCommand, Unit>
{
    public Task<Result<Unit>> Handle(LoopPingCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => ping.Loop(command.Rounds, timeProvider.GetUtcNow()));
}
