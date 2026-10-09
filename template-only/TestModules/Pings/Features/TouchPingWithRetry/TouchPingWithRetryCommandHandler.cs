using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.TouchPingWithRetry;

internal sealed class TouchPingWithRetryCommandHandler(
    IPingRepository pingRepository,
    PingTestHooks pingTestHooks,
    TimeProvider timeProvider)
    : ICommandHandler<TouchPingWithRetryCommand, Unit>
{
    public Task<Result<Unit>> Handle(TouchPingWithRetryCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .TapAsync(_ => pingTestHooks.AfterLoad())
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()));
}
