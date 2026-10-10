namespace Acme.Modules.Bad.Domain.Stamps;

internal static class Stamp
{
    public static DateTimeOffset Now() => DateTimeOffset.UtcNow;
}
