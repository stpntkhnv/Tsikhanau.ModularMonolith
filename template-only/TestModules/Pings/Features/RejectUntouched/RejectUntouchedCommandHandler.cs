using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RejectUntouched;

internal sealed class RejectUntouchedCommandHandler(IPingRepository pingRepository)
    : ICommandHandler<RejectUntouchedCommand, Unit>
{
    public Task<Result<Unit>> Handle(RejectUntouchedCommand command, CancellationToken cancellationToken) =>
        pingRepository
            .GetAsync(PingId.From(command.PingId), cancellationToken)
            .BindAsync(_ => Result.Failure(PingErrors.Rejected));
}
