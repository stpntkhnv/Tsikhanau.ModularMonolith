using Tsikhanau.Monolith.Contracts.IntegrationEvents;

namespace Tsikhanau.Monolith.Shared.Fixture;

public static class EventNames
{
    public static String GetName(IntegrationEvent integrationEvent) => integrationEvent.GetType().Name;
}
