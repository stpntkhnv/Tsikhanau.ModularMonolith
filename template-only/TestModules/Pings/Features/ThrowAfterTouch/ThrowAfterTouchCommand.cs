using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ThrowAfterTouch;

internal sealed record ThrowAfterTouchCommand(Guid PingId) : IPingsCommand<Unit>;
