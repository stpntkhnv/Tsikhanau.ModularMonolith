using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.CreatePing;

internal sealed record CreatePingRequest(String Label, Optional<String> Nickname);
