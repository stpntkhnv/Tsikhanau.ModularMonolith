using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

[IntegrationEventName(Name)]
internal sealed record PingOfALaterReleaseIntegrationEvent(DateTimeOffset OccurredOn, Guid PingId, String Label)
    : IntegrationEvent(OccurredOn)
{
    public const String Name = "tests.ping-of-a-later-release";
}
