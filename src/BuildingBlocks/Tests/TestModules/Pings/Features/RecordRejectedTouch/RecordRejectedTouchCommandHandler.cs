using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RecordRejectedTouch;

internal sealed class RecordRejectedTouchCommandHandler(IPingRepository pingRepository, TimeProvider timeProvider)
    : ICommandHandler<RecordRejectedTouchCommand, Unit>
{
    public Task<Result<Unit>> Handle(RecordRejectedTouchCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(ping => ping.Touch(timeProvider.GetUtcNow()))
            .BindAsync(_ => Result.Failure(PingErrors.Rejected));
}
