namespace Acme.Modules.Bad.Domain.Stamps;

internal sealed class StampLedger
{
    public DateTimeOffset this[TimeProvider clock] => DateTimeOffset.UnixEpoch;

    public static StampLedger operator +(StampLedger stampLedger, TimeProvider timeProvider) => stampLedger;
}
