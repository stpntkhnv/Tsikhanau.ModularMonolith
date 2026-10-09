using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RejectAfterTouch;

internal sealed record RejectAfterTouchCommand(Guid PingId) : IPingsCommand<Unit>;
