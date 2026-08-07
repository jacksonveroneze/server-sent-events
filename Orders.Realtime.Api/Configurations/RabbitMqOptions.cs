namespace Orders.Realtime.Api.Configurations;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string QueueName { get; set; } = "quotations.stream";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
