using System.Text.Json.Serialization;

namespace ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

public abstract record IntegrationEvent(DateTimeOffset OccurredOn)
{
    [JsonInclude]
    [JsonRequired]
    public Guid Id { get; private init; } = Guid.CreateVersion7(OccurredOn);
}
