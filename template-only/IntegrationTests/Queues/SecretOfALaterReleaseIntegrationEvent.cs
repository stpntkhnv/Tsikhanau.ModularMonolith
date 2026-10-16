using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Secrets;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

[IntegrationEventName(Name)]
[ContainsSecrets]
internal sealed record SecretOfALaterReleaseIntegrationEvent(DateTimeOffset OccurredOn, String Secret)
    : IntegrationEvent(OccurredOn)
{
    public const String Name = "tests.secret-of-a-later-release";
}
