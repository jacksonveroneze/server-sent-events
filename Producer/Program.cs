using Quotations.Realtime.Producer;
using Quotations.Realtime.Shared.Configurations;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<QuotationProducerService>();

var host = builder.Build();

host.Run();
