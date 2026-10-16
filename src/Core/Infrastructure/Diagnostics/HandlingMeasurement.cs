namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

internal readonly struct HandlingMeasurement(
    QueueInstruments queueInstruments,
    TimeProvider timeProvider,
    Int64 startedTimestamp)
    : IDisposable
{
    public void Dispose() => queueInstruments.RecordHandlingDuration(timeProvider.GetElapsedTime(startedTimestamp));
}
