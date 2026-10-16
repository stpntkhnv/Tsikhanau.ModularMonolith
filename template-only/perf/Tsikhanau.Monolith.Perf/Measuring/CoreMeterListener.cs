using System.Diagnostics.Metrics;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.Perf.Measuring;

internal sealed class CoreMeterListener : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly MeterListener _meterListener;
    private Int64 _measurementCount;
    private Int32 _instrumentCount;

    public CoreMeterListener(IMeterFactory meterFactory)
    {
        _meterFactory = meterFactory;
        _meterListener = new MeterListener { InstrumentPublished = EnableWhenOwned };
        _meterListener.SetMeasurementEventCallback<Int64>(CountMeasurement);
        _meterListener.SetMeasurementEventCallback<Double>(CountMeasurement);
        _meterListener.Start();
    }

    public Int64 MeasurementCount => Interlocked.Read(ref _measurementCount);

    public Int32 InstrumentCount => Volatile.Read(ref _instrumentCount);

    public static Int64 CountMeasurements(IEnumerable<CoreMeterListener> coreMeterListeners) =>
        coreMeterListeners.Sum(coreMeterListener => coreMeterListener.MeasurementCount);

    public static String BuildLine(
        IReadOnlyCollection<CoreMeterListener> coreMeterListeners,
        Int64 measurementCountBefore) =>
        $"  core meter: {CountInstruments(coreMeterListeners)} instruments listened to in "
            + $"{coreMeterListeners.Count} host(s), "
            + $"{CountMeasurements(coreMeterListeners) - measurementCountBefore} measurements during the run";

    public void Dispose() => _meterListener.Dispose();

    private static Int32 CountInstruments(IEnumerable<CoreMeterListener> coreMeterListeners) =>
        coreMeterListeners.Sum(coreMeterListener => coreMeterListener.InstrumentCount);

    private void EnableWhenOwned(Instrument instrument, MeterListener meterListener)
    {
        if (IsOwned(instrument))
        {
            meterListener.EnableMeasurementEvents(instrument);
            Interlocked.Increment(ref _instrumentCount);
        }
    }

    private Boolean IsOwned(Instrument instrument) =>
        instrument.Meter.Name == AppDiagnostics.SourceName && ReferenceEquals(instrument.Meter.Scope, _meterFactory);

    private void CountMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<String, Object?>> tags,
        Object? state)
        where T : struct =>
        Interlocked.Increment(ref _measurementCount);
}
