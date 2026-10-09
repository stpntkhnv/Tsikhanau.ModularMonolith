using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.CommittingInternalCommand;

[InternalCommandName("record-rejection")]
public sealed record RecordRejectionInternalCommand(Guid Id) : InternalCommand(Id), ICommitOnFailure;

public sealed class RecordRejectionInternalCommandHandler : ICommandHandler<RecordRejectionInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RecordRejectionInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
