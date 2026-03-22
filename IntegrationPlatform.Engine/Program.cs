using IntegrationPlatform.Engine.Applications.Interfaces;
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

builder.Services.AddScoped<ApiToKafkaHandler>();
builder.Services.AddScoped<KafkaToApiHandler>();
builder.Services.AddScoped<KafkaToKafkaHandler>();
builder.Services.AddScoped<KafkaToDatabaseHandler>();

builder.Services.AddScoped<KafkaMessageHandler>();
builder.Services.AddScoped<IKafkaMessageHandler, KafkaMessageHandler>();

builder.Services.AddHostedService<KafkaConsumer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KafkaConsumer>>();
    var configuration = sp.GetRequiredService<IConfiguration>();

    var bootstrapServers = configuration["Kafka:BootstrapServers"];
    if (string.IsNullOrEmpty(bootstrapServers))
    {
        logger.LogError("Kafka:BootstrapServers is not configured!");
        throw new InvalidOperationException("Kafka:BootstrapServers is required");
    }

    logger.LogInformation("Kafka configured with BootstrapServers: {BootstrapServers}", bootstrapServers);

    return new KafkaConsumer(logger, configuration, sp);
});

builder.Services.AddLogging();

var host = builder.Build();
host.Run();