using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.AddEcho;

internal sealed record AddEchoCommand(Guid PingId, Guid EchoId, String Text) : IPingsCommand<Unit>;
