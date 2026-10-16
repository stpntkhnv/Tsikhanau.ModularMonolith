using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed class MetricRecorder : IDisposable
{
    private readonly ConcurrentQueue<RecordedMeasurement> _recorded = new();
    private readonly ConcurrentQueue<RecordedMeasurement> _observed = new();
    private readonly IMeterFactory _meterFactory;
    private readonly MeterListener _meterListener;

    private MetricRecorder(IMeterFactory meterFactory)
    {
        _meterFactory = meterFactory;
        _meterListener = new MeterListener { InstrumentPublished = EnableWhenOwned };
        _meterListener.SetMeasurementEventCallback<Int64>(Record);
        _meterListener.SetMeasurementEventCallback<Double>(Record);
        _meterListener.Start();
    }

    public IReadOnlyList<RecordedMeasurement> Recorded => _recorded.ToList();

    public static MetricRecorder StartFor(IServiceProvider serviceProvider) =>
        new(serviceProvider.GetRequiredService<IMeterFactory>());

    public IReadOnlyList<RecordedMeasurement> ReadRecorded(String instrumentName) =>
        _recorded
            .Where(measurement => measurement.Instrument == instrumentName)
            .ToList();

    public IReadOnlyList<RecordedMeasurement> Observe(String instrumentName)
    {
        _observed.Clear();
        _meterListener.RecordObservableInstruments();
        return _observed
            .Where(measurement => measurement.Instrument == instrumentName)
            .ToList();
    }

    public void Dispose() => _meterListener.Dispose();

    private static Dictionary<String, String> ReadTags(ReadOnlySpan<KeyValuePair<String, Object?>> tags)
    {
        var tagValues = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            tagValues[tag.Key] = tag.Value?.ToString() ?? String.Empty;
        }

        return tagValues;
    }

    private void EnableWhenOwned(Instrument instrument, MeterListener meterListener)
    {
        if (IsOwned(instrument))
        {
            meterListener.EnableMeasurementEvents(instrument);
        }
    }

    private Boolean IsOwned(Instrument instrument) =>
        instrument.Meter.Name == AppDiagnostics.SourceName && ReferenceEquals(instrument.Meter.Scope, _meterFactory);

    private void Record<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<String, Object?>> tags,
        Object? state)
        where T : struct, INumberBase<T>
    {
        var recordedMeasurement = new RecordedMeasurement(
            instrument.Name,
            Double.CreateTruncating(measurement),
            ReadTags(tags));

        if (instrument.IsObservable)
        {
            _observed.Enqueue(recordedMeasurement);
            return;
        }

        _recorded.Enqueue(recordedMeasurement);
    }
}
