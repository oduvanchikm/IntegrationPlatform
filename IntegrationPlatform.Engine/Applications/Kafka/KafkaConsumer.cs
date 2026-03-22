using Confluent.Kafka;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(
    ILogger<KafkaConsumer> logger,
    IConfiguration configuration,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly int _reconnectDelayMs = 5000;
    private readonly int _maxReconnectAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Kafka Consumer is starting.");

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var topic = configuration["Kafka:Topic"];
        var groupId = configuration["Kafka:GroupId"];

        int attempt = 0;

        while (!stoppingToken.IsCancellationRequested && attempt < _maxReconnectAttempts)
        {
            try
            {
                var config = new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers,
                    GroupId = groupId,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                    SessionTimeoutMs = 6000,
                    MaxPollIntervalMs = 300000,
                    SocketTimeoutMs = 60000,
                    ReconnectBackoffMs = 1000,
                    ReconnectBackoffMaxMs = 10000
                };

                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(topic);

                logger.LogInformation("Kafka consumer started. Listening topic {Topic} on {BootstrapServers}",
                    topic, bootstrapServers);

                attempt = 0; // Сброс счетчика при успешном подключении

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));

                        if (consumeResult?.Message?.Value == null)
                            continue;

                        logger.LogInformation("Message received from topic {Topic} at offset {Offset}",
                            consumeResult.Topic, consumeResult.Offset);

                        using var scope = serviceProvider.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<KafkaMessageHandler>();

                        await handler.HandleMessageAsync(consumeResult.Message.Value);

                        consumer.Commit(consumeResult);
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                        await Task.Delay(1000, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unexpected error processing message");
                        await Task.Delay(1000, stoppingToken);
                    }
                }

                consumer.Close();
            }
            catch (Exception ex)
            {
                attempt++;
                logger.LogWarning(ex,
                    "Failed to connect to Kafka (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms...",
                    attempt, _maxReconnectAttempts, _reconnectDelayMs);

                await Task.Delay(_reconnectDelayMs, stoppingToken);
            }
        }

        if (attempt >= _maxReconnectAttempts)
        {
            logger.LogError("Failed to connect to Kafka after {MaxAttempts} attempts", _maxReconnectAttempts);
        }
    }
}