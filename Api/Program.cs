using Quotations.Realtime.Api.Endpoints;
using Quotations.Realtime.Api.Services.Quotations;
using Quotations.Realtime.Shared.Configurations;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<QuotationBroadcaster>();
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<QuotationConsumerService>();

builder.Services.AddCors()
    .AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.UseCors(p => p.AllowAnyHeader()
    .AllowAnyMethod()
    .AllowAnyOrigin());

app.MapQuotationsEndpoints();

app.Run();