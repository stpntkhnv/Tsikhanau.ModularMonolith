using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RecordRejectedTouch;

internal sealed record RecordRejectedTouchCommand(Guid PingId) : IPingsCommand<Unit>, ICommitOnFailure;
