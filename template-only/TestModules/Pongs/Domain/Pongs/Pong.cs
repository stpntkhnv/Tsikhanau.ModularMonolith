using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs.Events;

namespace Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

internal sealed class Pong : AggregateRoot<PongId>
{
    private Pong(PongId id) : base(id) { }

    public Guid PingId { get; private set; }

    public PongLabel Label { get; private set; }

    public static Pong Create(PongId id, Guid pingId, PongLabel label, DateTimeOffset createdOn)
    {
        var pong = new Pong(id) { PingId = pingId, Label = label };
        pong.Raise(new PongCreatedDomainEvent(createdOn, id));
        return pong;
    }
}
