using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ThrowAfterTouch;

internal sealed class ThrowAfterTouchCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<ThrowAfterTouchCommand, Unit>
{
    public Task<Result<Unit>> Handle(ThrowAfterTouchCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()))
            .BindAsync(Throw);

    private static Result<Unit> Throw(Unit _) =>
        throw new InvalidOperationException("Handler failed after changing the ping");
}
