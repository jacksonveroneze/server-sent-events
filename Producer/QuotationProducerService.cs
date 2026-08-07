using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quotations.Realtime.Shared.Configurations;
using Quotations.Realtime.Shared.Models;
using RabbitMQ.Client;

namespace Quotations.Realtime.Producer;

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
            UserName = rabbitMqOptions.UserName,
            Password = rabbitMqOptions.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: rabbitMqOptions.ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
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
                    Value: Math.Round((decimal)(random.Next(-50, 50)), 2)
                );

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(quotation));

                await _channel!.BasicPublishAsync(
                    exchange: rabbitMqOptions.ExchangeName,
                    routingKey: string.Empty,
                    body: body,
                    cancellationToken: stoppingToken);

                logger.LogInformation(
                    "Published quotation: {TickerId} for {Value}", quotation.TickerId, quotation.Value);

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
