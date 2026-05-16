using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Applications.Kafka;
using IntegrationPlatform.Engine.Applications.Orchestration;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Prometheus;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PublicationDbContext"));
});


builder.Services.AddDbContextFactory<SubscriptionDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubscriptionDbContext"));
});

builder.Services.AddScoped<KafkaReader>();
builder.Services.AddScoped<KafkaWriter>();
builder.Services.AddScoped<ApiReader>();
builder.Services.AddScoped<ApiWriter>();
builder.Services.AddScoped<DatabaseReader>();
builder.Services.AddScoped<DatabaseWriter>();

builder.Services.AddScoped<OrchestrationService>();

builder.Services.AddScoped<KafkaToKafkaHandler>();
builder.Services.AddScoped<KafkaToApiHandler>();
builder.Services.AddScoped<KafkaToDatabaseHandler>();
builder.Services.AddScoped<ApiToKafkaHandler>();
builder.Services.AddScoped<ApiToApiHandler>();
builder.Services.AddScoped<ApiToDatabaseHandler>();
builder.Services.AddScoped<DatabaseToKafkaHandler>();
builder.Services.AddScoped<DatabaseToApiHandler>();
builder.Services.AddScoped<DatabaseToDatabaseHandler>();

builder.Services.AddScoped<KafkaMessageHandler>();
builder.Services.AddScoped<IKafkaMessageHandler, KafkaMessageHandler>();

builder.Services.AddHostedService<KafkaConsumer>();

builder.Services.AddLogging();

var host = builder.Build();

var metricServer = new MetricServer(port: 9091);
metricServer.Start();

host.Run();