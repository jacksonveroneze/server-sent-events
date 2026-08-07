namespace Orders.Realtime.Api.Configurations;

public record RabbitMqOptions
{
    public required string HostName { get; init; }

    public required string VirtualHost { get; init; }

    public required string QueueName { get; init; }

    public required string UserName { get; init; }
    
    public required string Password { get; init; }
}
