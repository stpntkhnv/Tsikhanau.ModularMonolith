namespace ModularMonolith.TestModules.Pings.Contracts.Queries.GetPing;

public sealed record PingDto(Guid Id, String Label, Int32 Touches, Int32 Version);
