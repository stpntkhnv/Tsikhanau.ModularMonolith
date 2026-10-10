using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;

public sealed record PingDto(Guid Id, String Label, Optional<String> Nickname, Int32 Touches, Int32 Version);
