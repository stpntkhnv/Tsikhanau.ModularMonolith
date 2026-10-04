using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.AddEcho;

internal sealed class AddEchoCommandHandler(IPingRepository pingRepository, PingTestHooks pingTestHooks)
    : ICommandHandler<AddEchoCommand, Unit>
{
    public Task<Result<Unit>> Handle(AddEchoCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.AddEcho(EchoId.From(command.EchoId), command.Text));
}
