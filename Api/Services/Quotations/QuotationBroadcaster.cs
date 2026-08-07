using System.Collections.Concurrent;
using System.Threading.Channels;
using Quotations.Realtime.Api.Models;

namespace Quotations.Realtime.Api.Services.Quotations;

public class QuotationBroadcaster(
    ILogger<QuotationBroadcaster> logger)
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

        logger.LogInformation("Client {ConnectionId} subscribed to quotations", connectionId);
        logger.LogInformation("Total subscribers {SubscriberCount}", _subscribers.Count);

        return (connectionId, channel.Reader);
    }

    public void Unsubscribe(string id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            logger.LogInformation("Client {ConnectionId} unsubscribed from quotations", id);
            logger.LogInformation("Total subscribers {SubscriberCount}", _subscribers.Count);

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