using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.ThrowAfterTouch;

internal sealed record ThrowAfterTouchCommand(Guid PingId) : IPingsCommand<Unit>;
