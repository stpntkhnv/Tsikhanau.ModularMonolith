using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;

internal sealed record CreatePingCommand(Guid PingId, String Label, Optional<String> Nickname) : IPingsCommand<Guid>;
