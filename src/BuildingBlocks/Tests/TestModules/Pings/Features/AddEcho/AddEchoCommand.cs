using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Features.AddEcho;

internal sealed record AddEchoCommand(Guid PingId, Guid EchoId, String Text) : IPingsCommand<Unit>;
