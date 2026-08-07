using System.Net.ServerSentEvents;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Orders.Realtime.Api.Models;
using Orders.Realtime.Api.Services;
using Orders.Realtime.Api.Services.Orders;

namespace Orders.Realtime.Api.Endpoints;

public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("orders/realtime", (
            ChannelReader<OrderPlacement> channelReader,
            CancellationToken cancellationToken) =>
        {
            return Results.ServerSentEvents(
                channelReader.ReadAllAsync(cancellationToken),
                "orders");
        });

        app.MapGet("orders/realtime/with-events", (
            ChannelReader<OrderPlacement> channelReader,
            OrderEventBuffer eventBuffer,
            [FromHeader(Name = "Last-Event-ID")] string? lastEventId,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.ServerSentEvents(
                StreamEvents(), "orders");

            async IAsyncEnumerable<SseItem<OrderPlacement>> StreamEvents()
            {
                if (!string.IsNullOrWhiteSpace(lastEventId))
                {
                    var missedEvents = eventBuffer.GetEventsAfter(lastEventId);

                    foreach (var missedEvent in missedEvents)
                    {
                        yield return missedEvent;
                    }
                }

                await foreach (var order in channelReader
                                   .ReadAllAsync(cancellationToken))
                {
                    yield return eventBuffer.Add(order);
                }
            }
        });
    }
}