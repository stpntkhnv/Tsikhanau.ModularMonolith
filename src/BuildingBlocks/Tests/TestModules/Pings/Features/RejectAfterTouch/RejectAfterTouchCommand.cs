using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RejectAfterTouch;

internal sealed record RejectAfterTouchCommand(Guid PingId) : IPingsCommand<Unit>;
