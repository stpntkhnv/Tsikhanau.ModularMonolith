using ModularMonolith.BuildingBlocks.Domain.Events;

namespace ModularMonolith.TestModules.Pongs.Domain.Pongs.Events;

internal sealed record PongCreatedDomainEvent(DateTimeOffset OccurredOn, PongId PongId) : DomainEvent(OccurredOn);
