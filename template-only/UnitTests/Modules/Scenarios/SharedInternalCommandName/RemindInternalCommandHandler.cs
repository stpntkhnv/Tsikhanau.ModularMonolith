using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.SharedInternalCommandName;

public sealed class RemindInternalCommandHandler : ICommandHandler<RemindInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RemindInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
