using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Quotations.Realtime.Api.Configurations;
using Quotations.Realtime.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Quotations.Realtime.Api.Services.Quotations;

public class QuotationConsumerService(
    QuotationBroadcaster broadcaster,
    IOptions<RabbitMqOptions> options,
    ILogger<QuotationConsumerService> logger)
    : BackgroundService
{
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

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 20,
            global: false,
            cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var quotation = JsonSerializer.Deserialize<Quotation>(Encoding.UTF8.GetString(ea.Body.Span));

                if (quotation is not null)
                {
                    broadcaster.Publish(quotation);

                    logger.LogInformation(
                        "Consumed quotation: {TickerId} for {Value}", quotation.TickerId, quotation.Value);
                }

                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing quotation message, sending to dead-letter");
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        };

        var rabbitMqOptions = options.Value;

        await _channel!.BasicConsumeAsync(
            queue: rabbitMqOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
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
