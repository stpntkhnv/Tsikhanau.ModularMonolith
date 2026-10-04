using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.LoopPing;

internal sealed record LoopPingCommand(Guid PingId, Int32 Rounds) : IPingsCommand<Unit>;
