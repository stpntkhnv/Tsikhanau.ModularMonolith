using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

public sealed class QueueMetrics
{
    private const String MessageUnit = "{message}";

    private const String SecondUnit = "s";

    private static readonly InstrumentAdvice<Double> HandlingDurationAdvice = new()
    {
        HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10, 30, 60],
    };

    private readonly ConcurrentDictionary<(String Module, String Queue), QueueSample> _lastSamples = new();
    private readonly ModuleRegistry _moduleRegistry;
    private readonly Counter<Int64> _messageCounter;
    private readonly Histogram<Double> _handlingDurationHistogram;

    internal QueueMetrics(IMeterFactory meterFactory, ModuleRegistry moduleRegistry)
    {
        _moduleRegistry = moduleRegistry;
        var meter = meterFactory.Create(AppDiagnostics.SourceName);
        _messageCounter = meter.CreateCounter<Int64>(
            "tsikhanau.queue.messages",
            MessageUnit,
            "Queue messages by the outcome that their row update recorded");

        _handlingDurationHistogram = meter.CreateHistogram(
            "tsikhanau.queue.handling.duration",
            SecondUnit,
            "Time a queue handler ran, with its commit in a transaction",
            tags: null,
            HandlingDurationAdvice);

        meter.CreateObservableGauge(
            "tsikhanau.queue.pending",
            ObservePendingCounts,
            MessageUnit,
            "Messages neither processed nor failed, at the last sample");

        meter.CreateObservableGauge(
            "tsikhanau.queue.failed",
            ObserveFailedCounts,
            MessageUnit,
            "Failed messages kept in the queue, at the last sample");

        meter.CreateObservableGauge(
            "tsikhanau.queue.oldest_due.age",
            ObserveOldestDueAges,
            SecondUnit,
            "Time since the oldest due pending message became due, at the last sample");
    }

    internal QueueInstruments CreateInstruments(Type moduleKey, String queue) =>
        new(_messageCounter, _handlingDurationHistogram, FindModuleName(moduleKey), queue);

    internal void Store(IEnumerable<QueueSample> samples) =>
        samples.ForEach(sample => _lastSamples[(sample.Module, sample.Queue)] = sample);

    private String FindModuleName(Type moduleKey) =>
        _moduleRegistry.Modules
            .Single(module => module.Key == moduleKey)
            .Name;

    private IEnumerable<Measurement<Int64>> ObservePendingCounts() =>
        _lastSamples.Values.Select(sample => new Measurement<Int64>(sample.PendingCount, sample.Tags));

    private IEnumerable<Measurement<Int64>> ObserveFailedCounts() =>
        _lastSamples.Values.Select(sample => new Measurement<Int64>(sample.FailedCount, sample.Tags));

    private IEnumerable<Measurement<Double>> ObserveOldestDueAges() =>
        _lastSamples.Values.Select(sample => new Measurement<Double>(sample.OldestDueAge.TotalSeconds, sample.Tags));
}
