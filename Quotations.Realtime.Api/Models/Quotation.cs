namespace Orders.Realtime.Api.Models;

public record Quotation(
    string TickerId,
    decimal Value
);