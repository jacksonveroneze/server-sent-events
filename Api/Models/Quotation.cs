namespace Quotations.Realtime.Api.Models;

public record Quotation(
    string TickerId,
    decimal Value
);