namespace ModularMonolith.BuildingBlocks.Domain.Events;

public abstract record DomainEvent(DateTimeOffset OccurredOn)
{
    public Guid Id { get; } = Guid.CreateVersion7(OccurredOn);
}
