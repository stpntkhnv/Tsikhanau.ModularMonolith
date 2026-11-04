namespace Acme.Modules.Bad.Domain.Stamps;

internal static class StampSchedule
{
    public static Func<DateTime> Plan() => () => DateTime.UtcNow;
}
