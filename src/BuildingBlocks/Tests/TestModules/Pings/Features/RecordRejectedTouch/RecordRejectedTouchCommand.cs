using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RecordRejectedTouch;

internal sealed record RecordRejectedTouchCommand(Guid PingId) : IPingsCommand<Unit>, ICommitOnFailure;
