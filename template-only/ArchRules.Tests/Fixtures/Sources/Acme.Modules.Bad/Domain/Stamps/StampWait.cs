namespace Acme.Modules.Bad.Domain.Stamps;

internal static class StampWait
{
    public static async Task<DateTimeOffset> ReadLaterAsync()
    {
        await Task.Yield();
        return DateTimeOffset.Now;
    }
}
