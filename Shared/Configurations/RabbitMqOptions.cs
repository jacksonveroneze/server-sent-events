namespace Quotations.Realtime.Shared.Configurations;

public record RabbitMqOptions
{
    public required string HostName { get; init; }

    public required string VirtualHost { get; init; }

    public required string ExchangeName { get; init; }

    public required string UserName { get; init; }

    public required string Password { get; init; }
}
