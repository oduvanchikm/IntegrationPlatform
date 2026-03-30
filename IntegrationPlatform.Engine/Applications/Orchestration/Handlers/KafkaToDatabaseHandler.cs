using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToDatabaseHandler(
    ILogger<KafkaToDatabaseHandler> logger,
    KafkaReader kafkaReader,
    DatabaseWriter databaseWriter)
{
    private CancellationTokenSource _cts = new();

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== KAFKA TO DATABASE HANDLER ==========");

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

        await kafkaReader.StreamFromKafkaAsync(sourceKafka, async (message) =>
        {
            logger.LogInformation("Received message from Kafka, writing to database");
            await databaseWriter.WriteToDatabaseAsync(targetDb, new List<string> { message });
            logger.LogDebug("Message written to database");
        }, _cts.Token);
    }

    public void Stop()
    {
        _cts.Cancel();
    }
}