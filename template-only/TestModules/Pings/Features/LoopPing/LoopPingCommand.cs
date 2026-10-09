using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.LoopPing;

internal sealed record LoopPingCommand(Guid PingId, Int32 Rounds) : IPingsCommand<Unit>;
