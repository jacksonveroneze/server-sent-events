using System.Collections.Concurrent;
using System.Threading.Channels;
using Orders.Realtime.Api.Models;

namespace Orders.Realtime.Api.Services.Quotations;

public class QuotationBroadcaster
{
    private readonly ConcurrentDictionary<string, Channel<Quotation>> _subscribers = new();

    public (string Id, ChannelReader<Quotation> Reader) Subscribe(string connectionId)
    {
        var channel = Channel.CreateBounded<Quotation>(
            new BoundedChannelOptions(15)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        _subscribers[connectionId] = channel;

        return (connectionId, channel.Reader);
    }

    public void Unsubscribe(string id)
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