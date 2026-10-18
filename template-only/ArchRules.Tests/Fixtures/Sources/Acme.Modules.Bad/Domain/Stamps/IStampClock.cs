using System.Diagnostics;

namespace Acme.Modules.Bad.Domain.Stamps;

internal interface IStampClock
{
    TimeProvider Clock { get; }

    Stopwatch StartLap();

    void Wind(TimeProvider timeProvider);
}
