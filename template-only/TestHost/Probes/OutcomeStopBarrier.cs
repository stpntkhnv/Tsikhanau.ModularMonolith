using System.Diagnostics.Metrics;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class OutcomeStopBarrier : IHostedService, IDisposable
{
    public const String ReleasedMarker = "probe: stop barrier released on";

    public const String TimedOutMarker = "probe: stop barrier held until the shutdown timeout";

    private const String MessagesInstrument = "tsikhanau.queue.messages";

    private const String ModuleTag = "module";

    private const String QueueTag = "queue";

    private const String OutcomeTag = "outcome";

    private readonly TaskCompletionSource<String> _outcomeSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly MeterListener _meterListener;
    private readonly String _module;
    private readonly String _queue;

    public OutcomeStopBarrier(String module, String queue)
    {
        _module = module;
        _queue = queue;
        _meterListener = new MeterListener { InstrumentPublished = EnableWhenItCountsMessages };
        _meterListener.SetMeasurementEventCallback<Int64>(RecordOutcome);
        _meterListener.Start();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _outcomeSource.Task.WaitAsync(cancellationToken);
            await Console.Out.WriteLineAsync($"{ReleasedMarker} {outcome}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Console.Out.WriteLineAsync(TimedOutMarker);
        }
    }

    public void Dispose() => _meterListener.Dispose();

    private static void EnableWhenItCountsMessages(Instrument instrument, MeterListener meterListener)
    {
        if (instrument.Meter.Name == AppDiagnostics.SourceName && instrument.Name == MessagesInstrument)
        {
            meterListener.EnableMeasurementEvents(instrument);
        }
    }

    private static String ReadTag(ReadOnlySpan<KeyValuePair<String, Object?>> tags, String key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
            {
                return tag.Value?.ToString() ?? String.Empty;
            }
        }

        return String.Empty;
    }

    private void RecordOutcome(
        Instrument instrument,
        Int64 measurement,
        ReadOnlySpan<KeyValuePair<String, Object?>> tags,
        Object? state)
    {
        if (ReadTag(tags, ModuleTag) == _module && ReadTag(tags, QueueTag) == _queue)
        {
            _outcomeSource.TrySetResult(ReadTag(tags, OutcomeTag));
        }
    }
}
