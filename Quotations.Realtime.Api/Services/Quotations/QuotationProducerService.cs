using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orders.Realtime.Api.Configurations;
using Orders.Realtime.Api.Models;
using RabbitMQ.Client;

namespace Orders.Realtime.Api.Services.Quotations;

public class QuotationProducerService(
    IOptions<RabbitMqOptions> options,
    ILogger<QuotationProducerService> logger)
    : BackgroundService
{
    private static readonly string[] TickerIds =
    [
        "PETR4", "VALE3", "MGLU3", "AGRO4",
        "ITUB4", "VALE3", "IBOV", "NASDAQ"
    ];

    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var rabbitMqOptions = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = rabbitMqOptions.HostName,
            VirtualHost = rabbitMqOptions.VirtualHost,
            UserName = rabbitMqOptions.UserName,
            Password = rabbitMqOptions.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: rabbitMqOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitMqOptions = options.Value;
        var random = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var quotation = new Quotation(
                    TickerId: TickerIds[random.Next(TickerIds.Length)],
                    Value: Math.Round((decimal)(random.Next(-10, 10)), 2)
                );

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(quotation));

                await _channel!.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: rabbitMqOptions.QueueName,
                    body: body,
                    cancellationToken: stoppingToken);

                // logger.LogInformation(
                //     "Published quotation: {TickerId} for {Value}", quotation.TickerId, quotation.Value);

                await Task.Delay(TimeSpan.FromMilliseconds(750), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing quotation");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
