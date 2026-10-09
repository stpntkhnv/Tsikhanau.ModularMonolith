using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RemoveEcho;

internal sealed class RemoveEchoCommandHandler(IPingRepository pingRepository, PingTestHooks pingTestHooks)
    : ICommandHandler<RemoveEchoCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemoveEchoCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.RemoveEcho(EchoId.From(command.EchoId)));
}
