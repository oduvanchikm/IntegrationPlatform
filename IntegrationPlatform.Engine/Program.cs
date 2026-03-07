using IntegrationPlatform.Engine.Applications.Kafka;
using IntegrationPlatform.Engine.Applications.Orchestration;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Publication.DataAccess;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPublicationDbContext(
    builder.Configuration.GetConnectionString("PublicationDbContext"));

builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PublicationDbContext"));
});

builder.Services.AddSubscriptionDbContext(
    builder.Configuration.GetConnectionString("SubscriptionDbContext"));

builder.Services.AddDbContextFactory<SubscriptionDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubscriptionDbContext"));
});

builder.Services.AddScoped<OrchestrationService>();
builder.Services.AddScoped<KafkaMessageHandler>();

builder.Services.AddScoped<ApiToKafkaHandler>();
builder.Services.AddScoped<KafkaToApiHandler>();
builder.Services.AddScoped<KafkaToKafkaHandler>();
builder.Services.AddScoped<KafkaToDatabaseHandler>();

builder.Services.AddHostedService<KafkaConsumer>();

builder.Services.AddLogging();

var host = builder.Build();
host.Run();
