using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Modules.Scenarios.CommittingInternalCommand;

[InternalCommandName("record-rejection")]
public sealed record RecordRejectionInternalCommand(Guid Id) : InternalCommand(Id), ICommitOnFailure;

public sealed class RecordRejectionInternalCommandHandler : ICommandHandler<RecordRejectionInternalCommand, Unit>
{
    public Task<Result<Unit>> Handle(RecordRejectionInternalCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
