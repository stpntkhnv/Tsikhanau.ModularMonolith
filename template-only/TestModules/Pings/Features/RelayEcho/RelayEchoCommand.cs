using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.RelayEcho;

internal sealed record RelayEchoCommand(Guid PingId, Guid TargetPingId, Guid EchoId, String Text)
    : IPingsCommand<Unit>;
