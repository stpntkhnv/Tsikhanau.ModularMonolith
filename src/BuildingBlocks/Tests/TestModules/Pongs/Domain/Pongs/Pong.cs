using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.TestModules.Pongs.Domain.Pongs.Events;

namespace ModularMonolith.TestModules.Pongs.Domain.Pongs;

internal sealed class Pong : AggregateRoot<PongId>
{
    public const Int32 MaxLabelLength = 50;

    private Pong(PongId id) : base(id) { }

    public Guid PingId { get; private set; }

    public String Label { get; private set; } = String.Empty;

    public static Pong Create(PongId id, Guid pingId, String label, DateTimeOffset createdOn)
    {
        var pong = new Pong(id) { PingId = pingId, Label = label };
        pong.Raise(new PongCreatedDomainEvent(createdOn, id));
        return pong;
    }
}
