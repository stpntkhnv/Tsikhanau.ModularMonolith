using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.TouchPing;

internal sealed class TouchPingCommandHandler(
    IPingRepository pingRepository,
    PingTestHooks pingTestHooks,
    TimeProvider timeProvider)
    : ICommandHandler<TouchPingCommand, Unit>
{
    public Task<Result<Unit>> Handle(TouchPingCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()));
}
