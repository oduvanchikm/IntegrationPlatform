using Confluent.Kafka;
using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Metrics;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(
    ILogger<KafkaConsumer> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    private const int ReconnectDelayMs = 5000;

    private static readonly Gauge KafkaConsumerLag = Prometheus.Metrics
        .CreateGauge("engine_kafka_consumer_lag",
            "Current consumer lag for Kafka topic",
            new GaugeConfiguration { LabelNames = new[] { "topic", "partition" } });

    private static readonly Counter MessagesConsumed = Prometheus.Metrics
        .CreateCounter("engine_kafka_messages_consumed_total",
            "Total messages consumed from Kafka",
            new CounterConfiguration { LabelNames = new[] { "topic" } });

    private static readonly Counter ConsumerErrors = Prometheus.Metrics
        .CreateCounter("engine_kafka_consumer_errors_total",
            "Total consumer errors",
            new CounterConfiguration { LabelNames = new[] { "topic", "error_type" } });

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
            EnableAutoCommit = false,
            StatisticsIntervalMs = 5000
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(topic);

                EngineMetrics.ActiveTasks.Inc();

                logger.LogInformation(
                    "Kafka consumer connected. Topic: {Topic}, Servers: {BootstrapServers}",
                    topic, bootstrapServers);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(stoppingToken);

                        if (consumeResult?.Message?.Value == null)
                            continue;

                        EngineMetrics.MessagesConsumed.WithLabels(topic).Inc();

                        logger.LogInformation(
                            "Message received from {Topic} at offset {Offset}",
                            consumeResult.Topic, consumeResult.Offset);

                        using var scope = scopeFactory.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IKafkaMessageHandler>();

                        await handler.HandleMessageAsync(consumeResult.Message.Value);

                        consumer.Commit(consumeResult);
                        EngineMetrics.EventsProcessed.WithLabels("kafka_consumer").Inc();
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                        ConsumerErrors.WithLabels(topic, "consume_error").Inc();

                        EngineMetrics.ProcessingErrors.WithLabels("kafka", "consume_error").Inc();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message");
                        ConsumerErrors.WithLabels(topic, "processing_error").Inc();
                        EngineMetrics.ProcessingErrors.WithLabels("kafka", "processing_error").Inc();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Kafka consumer stopping...");
                EngineMetrics.ActiveTasks.Dec();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kafka connection failed. Retrying in {Delay} ms...", ReconnectDelayMs);
                ConsumerErrors.WithLabels("unknown", "connection_error").Inc();

                await Task.Delay(ReconnectDelayMs, stoppingToken);
            }
        }

        EngineMetrics.ActiveTasks.Dec();
    }
}