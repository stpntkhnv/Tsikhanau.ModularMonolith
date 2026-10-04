using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RejectUntouched;

internal sealed record RejectUntouchedCommand(Guid PingId) : IPingsCommand<Unit>, ICommitOnFailure;
