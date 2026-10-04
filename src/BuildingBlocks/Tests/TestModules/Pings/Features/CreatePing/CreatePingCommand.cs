namespace ModularMonolith.TestModules.Pings.Features.CreatePing;

internal sealed record CreatePingCommand(Guid PingId, String Label) : IPingsCommand<Guid>;
