using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal static class MessageConflicts
{
    private const String ErasedPayloadReason =
        "has an erased payload: a secret message loses its payload at its final failure, so it cannot be retried; "
        + "discard it";

    public static Error Missing(MessageAddress message) =>
        Error.Conflict("Queues.MessageMissing", $"{message} is missing");

    public static Error NotFailed(MessageAddress message) =>
        Error.Conflict("Queues.MessageNotFailed", $"{message} has not failed: it is pending or processed");

    public static Error ErasedPayload(MessageAddress message) =>
        Error.Conflict("Queues.ErasedPayload", $"{message} {ErasedPayloadReason}");
}
