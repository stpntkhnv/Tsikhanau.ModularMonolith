using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RejectAfterTouch;

internal sealed class RejectAfterTouchCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<RejectAfterTouchCommand, Unit>
{
    public Task<Result<Unit>> Handle(RejectAfterTouchCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()))
            .BindAsync(_ => Result.Failure(PingErrors.Rejected));
}
