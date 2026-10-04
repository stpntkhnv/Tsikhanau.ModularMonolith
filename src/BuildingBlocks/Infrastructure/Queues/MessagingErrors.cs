using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Queues;

public static class MessagingErrors
{
    public static readonly Error InvalidPayload = Error.Failure(
        "Messaging.InvalidPayload",
        "Message payload could not be deserialized");

    public static Error UnknownType(String type) =>
        Error.Failure("Messaging.UnknownType", $"No message type is registered as '{type}'");

    public static Error ForeignPayload(Guid messageId) =>
        Error.Failure("Messaging.ForeignPayload", $"Payload does not carry the id of message {messageId}");
}
