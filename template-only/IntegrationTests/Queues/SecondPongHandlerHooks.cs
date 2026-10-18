using System.Collections.Concurrent;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal sealed class SecondPongHandlerHooks
{
    public Boolean FailsNextCall { get; set; }

    public ConcurrentQueue<Guid> ReceivedPingIds { get; } = new();
}
