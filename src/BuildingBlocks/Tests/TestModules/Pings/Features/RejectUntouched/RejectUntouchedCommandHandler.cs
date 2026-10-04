using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RejectUntouched;

internal sealed class RejectUntouchedCommandHandler(IPingRepository pingRepository)
    : ICommandHandler<RejectUntouchedCommand, Unit>
{
    public Task<Result<Unit>> Handle(RejectUntouchedCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(_ => Result.Failure(PingErrors.Rejected));
}
