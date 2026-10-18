namespace Acme.SharedKernel;

public sealed class SystemClock : TimeProvider
{
    public DateTimeOffset Today() => GetUtcNow();
}
