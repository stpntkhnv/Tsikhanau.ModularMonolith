using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Tsikhanau.Monolith.Infrastructure.Queues;

public sealed class QueueSignals
{
    private readonly ConcurrentDictionary<Type, Channel<Boolean>> _channels = new();

    public void Signal(Type moduleType) =>
        GetChannel(moduleType).Writer
            .TryWrite(true);

    public ChannelReader<Boolean> GetReader(Type moduleType) => GetChannel(moduleType).Reader;

    private Channel<Boolean> GetChannel(Type moduleType) =>
        _channels.GetOrAdd(moduleType, static _ => Channel.CreateBounded<Boolean>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        }));
}
