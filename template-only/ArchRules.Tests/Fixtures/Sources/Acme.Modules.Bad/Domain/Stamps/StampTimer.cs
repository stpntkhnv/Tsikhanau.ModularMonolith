using System.Diagnostics;

namespace Acme.Modules.Bad.Domain.Stamps;

internal sealed class StampTimer
{
    private readonly Stopwatch _stopwatch;

    private StampTimer(Stopwatch stopwatch) => _stopwatch = stopwatch;
}
