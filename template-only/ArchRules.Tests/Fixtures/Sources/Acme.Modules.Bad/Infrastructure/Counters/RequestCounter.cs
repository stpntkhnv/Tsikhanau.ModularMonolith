namespace Acme.Modules.Bad.Infrastructure.Counters;

internal static class RequestCounter
{
    private static Int32 _count;

    public static Int32 Next() => ++_count;
}
