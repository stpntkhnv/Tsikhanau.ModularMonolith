using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.RemoveEcho;

internal sealed record RemoveEchoCommand(Guid PingId, Guid EchoId) : IPingsCommand<Unit>;
