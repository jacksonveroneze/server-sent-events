using System.Runtime.CompilerServices;
using Quotations.Realtime.Api.Models;
using Quotations.Realtime.Api.Services.Quotations;

namespace Quotations.Realtime.Api.Endpoints;

public static class QuotationEndpoints
{
    public static void MapQuotationsEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("quotations/realtime", (
            QuotationBroadcaster broadcaster,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var stream = StreamQuotations(
                broadcaster, context, cancellationToken);

            return Results.ServerSentEvents(stream, "quotations");
        });
    }

    private static async IAsyncEnumerable<Quotation> StreamQuotations(
        QuotationBroadcaster broadcaster,
        HttpContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (id, reader) = broadcaster.Subscribe(context.Connection.Id);

        try
        {
            await foreach (var quotation in reader
                               .ReadAllAsync(cancellationToken))
            {
                yield return quotation;
            }
        }
        finally
        {
            broadcaster.Unsubscribe(id);
        }
    }
}