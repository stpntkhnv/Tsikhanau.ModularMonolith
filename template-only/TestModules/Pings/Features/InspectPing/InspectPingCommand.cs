using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.InspectPing;

internal sealed record InspectPingCommand(Guid PingId) : IPingsCommand<Unit>;
