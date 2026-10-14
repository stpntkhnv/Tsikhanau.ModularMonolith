using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;

public sealed class RemindAgainInternalCommandHandler : ICommandHandler<RemindAgainInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemindAgainInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
