using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pongs.Domain.Pongs;

internal static class PongErrors
{
    public static readonly Error NotFound = Error.NotFound("Pong.NotFound", "Pong was not found");
}
