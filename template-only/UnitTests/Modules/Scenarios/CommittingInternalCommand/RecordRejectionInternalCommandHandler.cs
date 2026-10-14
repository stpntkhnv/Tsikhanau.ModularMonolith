using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.CommittingInternalCommand;

public sealed class RecordRejectionInternalCommandHandler : ICommandHandler<RecordRejectionInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RecordRejectionInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
