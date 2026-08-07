using System.Threading.Channels;
using Orders.Realtime.Api;
using Orders.Realtime.Api.Configurations;
using Orders.Realtime.Api.Endpoints;
using Orders.Realtime.Api.Models;
using Orders.Realtime.Api.Services;
using Orders.Realtime.Api.Services.Orders;
using Orders.Realtime.Api.Services.Quotations;

var builder = WebApplication.CreateBuilder(args);

var channel = Channel.CreateUnbounded<OrderPlacement>();
builder.Services.AddSingleton(channel);
builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

builder.Services.AddSingleton<OrderEventBuffer>();
builder.Services.AddHostedService<OrderProducerService>();

builder.Services.AddSingleton<QuotationBroadcaster>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<QuotationProducerService>();
builder.Services.AddHostedService<QuotationConsumerService>();

builder.Services.AddCors();

var app = builder.Build();

app.UseCors(p => p.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin());

app.MapOrdersEndpoints();
app.MapQuotationsEndpoints();

app.Run();