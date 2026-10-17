using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tsikhanau.Monolith.TestModules.Pongs.Domain.Pongs;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class PongWriteBlocker(TimeSpan blockDuration, CancellationToken observedToken)
    : SaveChangesInterceptor
{
    public const String BlocksMarker = "probe: pong written, its handler blocks";

    public const String EndsMarker = "probe: pong handler ends";

    public override async ValueTask<Int32> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        Int32 result,
        CancellationToken cancellationToken = default)
    {
        if (HasWrittenAPong(eventData.Context))
        {
            await BlockAsync();
        }

        return result;
    }

    private static Boolean HasWrittenAPong(DbContext? dbContext) =>
        dbContext is not null
        && dbContext.ChangeTracker
            .Entries<Pong>()
            .Any();

    private async Task BlockAsync()
    {
        await Console.Out.WriteLineAsync(BlocksMarker);
        try
        {
            await Task.Delay(blockDuration, observedToken);
        }
        finally
        {
            await Console.Out.WriteLineAsync(EndsMarker);
        }
    }
}
