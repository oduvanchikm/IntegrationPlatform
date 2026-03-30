using Confluent.Kafka;
using IntegrationPlatform.Engine.Applications.Interfaces;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(
    ILogger<KafkaConsumer> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    private const int ReconnectDelayMs = 5000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Kafka Consumer is starting.");

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var topic = configuration["Kafka:Topic"];
        var groupId = configuration["Kafka:GroupId"];

        logger.LogInformation(
            "Kafka config: Servers={Servers}, Topic={Topic}, Group={Group}",
            bootstrapServers, topic, groupId);

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(topic);

                logger.LogInformation(
                    "Kafka consumer connected. Topic: {Topic}, Servers: {BootstrapServers}",
                    topic, bootstrapServers);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value == null)
                        continue;

                    logger.LogInformation(
                        "Message received from {Topic} at offset {Offset}",
                        consumeResult.Topic, consumeResult.Offset);

                    using var scope = scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IKafkaMessageHandler>();

                    await handler.HandleMessageAsync(consumeResult.Message.Value);

                    consumer.Commit(consumeResult);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Kafka consumer stopping...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kafka connection failed. Retrying in {Delay} ms...", ReconnectDelayMs);

                await Task.Delay(ReconnectDelayMs, stoppingToken);
            }
        }
    }
}