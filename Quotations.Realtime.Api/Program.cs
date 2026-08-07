using Orders.Realtime.Api.Configurations;
using Orders.Realtime.Api.Endpoints;
using Orders.Realtime.Api.Services.Quotations;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<QuotationBroadcaster>();
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<QuotationProducerService>();
builder.Services.AddHostedService<QuotationConsumerService>();

builder.Services.AddCors();

var app = builder.Build();

app.UseCors(p => p.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin());

app.MapQuotationsEndpoints();

app.Run();