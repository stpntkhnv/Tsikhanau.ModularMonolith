using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.InspectPing;

internal sealed record InspectPingCommand(Guid PingId) : IPingsCommand<Unit>;
