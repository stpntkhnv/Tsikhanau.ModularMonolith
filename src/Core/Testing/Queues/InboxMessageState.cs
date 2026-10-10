using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Testing.Queues;

internal sealed record InboxMessageState(
    String Type,
    String Payload,
    Boolean IsProcessed,
    Boolean HasFailed,
    String LastError)
{
    public Boolean HasErasedPayload => Payload == QueueMessage.ErasedPayload;
}
