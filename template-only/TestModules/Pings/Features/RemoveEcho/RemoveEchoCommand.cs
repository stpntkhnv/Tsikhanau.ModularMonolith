using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RemoveEcho;

internal sealed record RemoveEchoCommand(Guid PingId, Guid EchoId) : IPingsCommand<Unit>;
