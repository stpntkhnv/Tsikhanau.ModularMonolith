using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;

internal static class PingErrors
{
    public static readonly Error TooManyTouches = Error.Conflict(
        "Ping.TooManyTouches",
        $"Ping cannot be touched more than {Ping.MaxTouches} times");

    public static readonly Error NotFound = Error.NotFound("Ping.NotFound", "Ping was not found");

    public static readonly Error Rejected = Error.Conflict("Ping.Rejected", "Ping change was rejected");

    public static readonly Error LabelTaken = Error.Conflict("Ping.LabelTaken", "Another ping already uses this label");

    public static readonly Error EchoNotFound = Error.NotFound("Ping.EchoNotFound", "Ping has no such echo");

    public static readonly Error AlreadyArchived = Error.Conflict("Ping.AlreadyArchived", "Ping is already archived");
}
