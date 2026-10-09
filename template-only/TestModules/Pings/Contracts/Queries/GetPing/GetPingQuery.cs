namespace Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;

public sealed record GetPingQuery(Guid PingId) : IPingsQuery<PingDto>;
