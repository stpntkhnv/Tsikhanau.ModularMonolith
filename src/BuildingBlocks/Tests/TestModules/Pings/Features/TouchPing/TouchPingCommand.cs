using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.TouchPing;

internal sealed record TouchPingCommand(Guid PingId) : IPingsCommand<Unit>;
