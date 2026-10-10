using Tsikhanau.Monolith.TestModules.Pings.Contracts.Queries.GetPing;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.GetPing;

internal sealed record PingRow(Guid Id, String Label, String? Nickname, Int32 Touches, Int32 Version)
{
    public PingDto ToDto() => new(Id, Label, Optional<String>.FromNullable(Nickname), Touches, Version);
}
