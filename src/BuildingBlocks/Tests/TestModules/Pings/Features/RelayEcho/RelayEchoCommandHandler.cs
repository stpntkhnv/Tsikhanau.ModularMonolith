using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RelayEcho;

internal sealed class RelayEchoCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<RelayEchoCommand, Unit>
{
    public Task<Result<Unit>> Handle(RelayEchoCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => RelayEcho(ping, command));

    private Result<Unit> RelayEcho(Ping ping, RelayEchoCommand command) =>
        ping.RelayEcho(
            PingId.From(command.TargetPingId),
            EchoId.From(command.EchoId),
            command.Text,
            timeProvider.GetUtcNow());
}
