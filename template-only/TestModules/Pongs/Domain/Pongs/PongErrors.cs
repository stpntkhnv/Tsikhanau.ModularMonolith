using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

internal static class PongErrors
{
    public static readonly Error NotFound = Error.NotFound("Pong.NotFound", "Pong was not found");
}
