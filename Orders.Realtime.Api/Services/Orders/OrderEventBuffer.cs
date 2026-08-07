using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using Orders.Realtime.Api.Models;

namespace Orders.Realtime.Api.Services.Orders;

public class OrderEventBuffer(int maxBufferSize = 100)
{
    private readonly ConcurrentQueue<SseItem<OrderPlacement>> _buffer = new();
    private long _nextEventId = 1;

    public SseItem<OrderPlacement> Add(OrderPlacement order)
    {
        var eventId = Interlocked.Increment(ref _nextEventId) - 1;
        var sseItem = new SseItem<OrderPlacement>(order)
        {
            EventId = eventId.ToString()
        };

        _buffer.Enqueue(sseItem);

        while (_buffer.Count > maxBufferSize)
        {
            _buffer.TryDequeue(out _);
        }

        return sseItem;
    }

    public IEnumerable<SseItem<OrderPlacement>> GetEventsAfter(string? lastEventId)
    {
        if (string.IsNullOrEmpty(lastEventId) || !long.TryParse(lastEventId, out var lastId))
        {
            return [];
        }

        return _buffer
            .Where(item => long.TryParse(item.EventId, out var itemId) && itemId > lastId)
            .OrderBy(item => long.Parse(item.EventId!));
    }
}
