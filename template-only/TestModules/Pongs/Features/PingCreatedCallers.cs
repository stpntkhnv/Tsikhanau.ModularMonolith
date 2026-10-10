using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features;

internal sealed record PingCreatedCallers(Guid PingId, Optional<String> CreatorId, Optional<String> HandlerCallerId);
