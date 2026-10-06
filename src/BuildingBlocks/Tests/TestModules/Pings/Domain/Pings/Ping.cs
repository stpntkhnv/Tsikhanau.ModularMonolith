using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Domain.Optionals;
using ModularMonolith.TestModules.Pings.Domain.Pings.Events;
using Tsikhanau.Railway;

namespace ModularMonolith.TestModules.Pings.Domain.Pings;

internal sealed class Ping : AggregateRoot<PingId>
{
    public const Int32 MaxTouches = 3;

    private readonly List<Echo> _echoes = [];

    private DateTimeOffset? _archivedOn;
    private PingLabel? _nickname;

    private Ping(PingId id) : base(id) { }

    public PingLabel Label { get; private set; }

    public Int32 Touches { get; private set; }

    public IReadOnlyList<Echo> Echoes => _echoes;

    public Optional<DateTimeOffset> ArchivedOn => _archivedOn.AsOptional();

    public Optional<PingLabel> Nickname => _nickname.AsOptional();

    public static Ping Create(PingId id, PingLabel label, DateTimeOffset createdOn)
    {
        var ping = new Ping(id) { Label = label };
        ping.Raise(new PingCreatedDomainEvent(createdOn, id, label));
        return ping;
    }

    public Result<Unit> Touch(DateTimeOffset touchedOn)
    {
        if (Touches >= MaxTouches)
        {
            return PingErrors.TooManyTouches;
        }

        Touches++;
        Raise(new PingTouchedDomainEvent(touchedOn, Id, Touches));
        return Unit.Value;
    }

    public Result<Unit> Loop(Int32 rounds, DateTimeOffset loopedOn)
    {
        if (rounds > 0)
        {
            Raise(new PingLoopedDomainEvent(loopedOn, Id, rounds - 1));
        }

        return Unit.Value;
    }

    public Result<Unit> RelayEcho(PingId targetPingId, EchoId echoId, String text, DateTimeOffset relayedOn)
    {
        Raise(new EchoRelayedDomainEvent(relayedOn, Id, targetPingId, echoId, text));
        return Unit.Value;
    }

    public Result<Unit> AddEcho(EchoId echoId, String text)
    {
        _echoes.Add(Echo.Create(echoId, text));
        return Unit.Value;
    }

    public Result<Unit> RemoveEcho(EchoId echoId) =>
        _echoes.RemoveAll(echo => echo.Id == echoId) > 0 ? Unit.Value : PingErrors.EchoNotFound;

    public Result<Unit> Archive(DateTimeOffset archivedOn)
    {
        if (_archivedOn.HasValue)
        {
            return PingErrors.AlreadyArchived;
        }

        _archivedOn = archivedOn;
        return Unit.Value;
    }

    public Result<Unit> GiveNickname(PingLabel nickname)
    {
        _nickname = nickname;
        return Unit.Value;
    }
}
