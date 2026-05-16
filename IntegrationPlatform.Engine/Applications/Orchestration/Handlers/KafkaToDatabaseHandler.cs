using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Engine.Metrics;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToDatabaseHandler(
    ILogger<KafkaToDatabaseHandler> logger,
    KafkaReader kafkaReader,
    DatabaseWriter databaseWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("========== KAFKA TO DATABASE HANDLER ==========");
        var pattern = "KafkaToDatabase";
        using var timer = EngineMetrics.ProcessingDuration.WithLabels(pattern).NewTimer();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceKafka = publicationInterface as KafkaInterface;
            var targetDb = subscriptionInterface as DatabaseInterface;

            if (sourceKafka == null || targetDb == null)
            {
                logger.LogError("Source is not Kafka or Target is not Database");
                return;
            }

            logger.LogInformation("Source Kafka: {BootstrapServers}, Topic={Topic}",
                sourceKafka.BootstrapServers, sourceKafka.TopicName);
            logger.LogInformation("Target DB: {Host}:{Port}/{Database}.{Schema}",
                targetDb.Host, targetDb.Port, targetDb.DatabaseName, targetDb.Scheme);

            await kafkaReader.ReadFromKafkaAsync(sourceKafka, async (message) =>
            {
                logger.LogInformation("Received message from Kafka, writing to database");
                await databaseWriter.WriteToDatabaseAsync(targetDb, new List<string> { message });
                logger.LogDebug("Message written to database");
            }, cancellationToken);
            EngineMetrics.EventsProcessed.WithLabels(pattern).Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while processing Kafka message");
            EngineMetrics.ProcessingErrors.WithLabels(pattern, "execution_error").Inc();
            throw;
        }
    }
}