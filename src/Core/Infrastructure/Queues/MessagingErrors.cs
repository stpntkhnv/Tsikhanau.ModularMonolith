using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

public static class MessagingErrors
{
    public static readonly Error InvalidPayload = Error.Failure(
        "Messaging.InvalidPayload",
        "Message payload could not be deserialized");

    public static Error UnknownType(String type) =>
        Error.Unexpected("Messaging.UnknownType", $"This instance has no message type registered as '{type}'");

    public static Error UnsubscribedType(String type) =>
        Error.Unexpected("Messaging.UnsubscribedType", $"This instance of the module does not subscribe to '{type}'");

    public static Error ForeignPayload(Guid messageId) =>
        Error.Failure("Messaging.ForeignPayload", $"Payload does not carry the id of message {messageId}");
}
