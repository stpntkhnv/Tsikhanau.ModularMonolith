using System.Text.Json.Serialization;

namespace Tsikhanau.Monolith.Contracts.IntegrationEvents;

public abstract record IntegrationEvent(DateTimeOffset OccurredOn)
{
    [JsonInclude]
    [JsonRequired]
    public Guid Id { get; private init; } = Guid.CreateVersion7(OccurredOn);
}
