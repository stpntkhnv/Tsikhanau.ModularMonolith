using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class QueueSignalsTests
{
    [Fact]
    public void A_burst_of_signals_collapses_into_one_wake_up()
    {
        var queueSignals = new QueueSignals();
        for (var signalNumber = 0; signalNumber < 500; signalNumber++)
        {
            queueSignals.Signal(typeof(QueueSignalsTests));
        }

        var reader = queueSignals.GetReader(typeof(QueueSignalsTests));
        var firstReadSucceeded = reader.TryRead(out _);
        var secondReadSucceeded = reader.TryRead(out _);

        firstReadSucceeded.ShouldBeTrue();
        secondReadSucceeded.ShouldBeFalse();
    }

    [Fact]
    public void Modules_do_not_share_a_signal()
    {
        var queueSignals = new QueueSignals();
        queueSignals.Signal(typeof(String));

        var isWoken = queueSignals
            .GetReader(typeof(Int32))
            .TryRead(out _);

        isWoken.ShouldBeFalse();
    }
}
