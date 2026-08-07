using System.Collections.Concurrent;
using System.Threading.Channels;
using Orders.Realtime.Api.Models;

namespace Orders.Realtime.Api.Services.Quotations;

public class QuotationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<Quotation>> _subscribers = new();

    public (Guid Id, ChannelReader<Quotation> Reader) Subscribe()
    {
        var channel = Channel.CreateBounded<Quotation>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;

        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public void Publish(Quotation quotation)
    {
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(quotation);
        }
    }
}