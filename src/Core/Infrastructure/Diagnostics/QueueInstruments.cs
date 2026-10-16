using System.Diagnostics.Metrics;

namespace Tsikhanau.Monolith.Infrastructure.Diagnostics;

internal sealed class QueueInstruments
{
    private const String Processed = "processed";

    private const String Retried = "retried";

    private const String Failed = "failed";

    private const String GivenBack = "given_back";

    private readonly Counter<Int64> _messageCounter;
    private readonly Histogram<Double> _handlingDurationHistogram;
    private readonly KeyValuePair<String, Object?>[] _queueTags;
    private readonly KeyValuePair<String, Object?>[] _processedTags;
    private readonly KeyValuePair<String, Object?>[] _retriedTags;
    private readonly KeyValuePair<String, Object?>[] _failedTags;
    private readonly KeyValuePair<String, Object?>[] _givenBackTags;

    public QueueInstruments(
        Counter<Int64> messageCounter,
        Histogram<Double> handlingDurationHistogram,
        String module,
        String queue)
    {
        _messageCounter = messageCounter;
        _handlingDurationHistogram = handlingDurationHistogram;
        _queueTags = QueueTags.Create(module, queue);
        _processedTags = QueueTags.Create(module, queue, Processed);
        _retriedTags = QueueTags.Create(module, queue, Retried);
        _failedTags = QueueTags.Create(module, queue, Failed);
        _givenBackTags = QueueTags.Create(module, queue, GivenBack);
    }

    public void CountProcessed(Int32 count) => Count(count, _processedTags);

    public void CountRetried(Int32 count) => Count(count, _retriedTags);

    public void CountFailed(Int32 count) => Count(count, _failedTags);

    public void CountGivenBack(Int32 count) => Count(count, _givenBackTags);

    public HandlingMeasurement MeasureHandling(TimeProvider timeProvider) =>
        new(this, timeProvider, timeProvider.GetTimestamp());

    public void RecordHandlingDuration(TimeSpan handlingDuration) =>
        _handlingDurationHistogram.Record(handlingDuration.TotalSeconds, _queueTags);

    private void Count(Int32 count, KeyValuePair<String, Object?>[] tags)
    {
        if (count > 0)
        {
            _messageCounter.Add(count, tags);
        }
    }
}
