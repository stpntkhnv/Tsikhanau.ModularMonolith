using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RemoveEcho;

internal sealed class RemoveEchoCommandHandler(IPingRepository pingRepository, PingTestHooks pingTestHooks)
    : ICommandHandler<RemoveEchoCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemoveEchoCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.RemoveEcho(EchoId.From(command.EchoId)));
}
