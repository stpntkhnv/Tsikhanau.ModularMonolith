using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.TouchPing;

internal sealed record TouchPingCommand(Guid PingId) : IPingsCommand<Unit>;
