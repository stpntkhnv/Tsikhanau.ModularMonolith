using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Acme.Modules.Bad.Domain.Leaks;

internal static class EventLeak
{
    public static Boolean IsPublished(IntegrationEvent integrationEvent) =>
        integrationEvent.OccurredOn > DateTimeOffset.MinValue;
}
