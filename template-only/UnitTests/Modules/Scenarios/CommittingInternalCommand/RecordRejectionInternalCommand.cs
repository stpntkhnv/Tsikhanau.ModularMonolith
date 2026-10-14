using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.UnitTests.Modules.Scenarios.CommittingInternalCommand;

[InternalCommandName("record-rejection")]
public sealed record RecordRejectionInternalCommand(Guid Id) : InternalCommand(Id), ICommitOnFailure;
