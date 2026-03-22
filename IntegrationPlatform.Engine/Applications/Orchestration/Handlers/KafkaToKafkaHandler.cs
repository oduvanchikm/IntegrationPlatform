using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToKafkaHandler(ILogger<KafkaToKafkaHandler> logger, KafkaReader kafkaReader, KafkaWriter kafkaWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== KAFKA TO KAFKA HANDLER EXECUTE START ==========");

        var sourceKafka = publicationInterface as KafkaInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceKafka == null || targetKafka == null)
        {
            logger.LogError("Kafka and Kafka are required.");
            return;
        }

        logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
                sourceKafka.BootstrapServers, sourceKafka.TopicName);
        logger.LogInformation("Target Kafka: BootstrapServers={BS}, Topic={Topic}",
                targetKafka.BootstrapServers, targetKafka.TopicName);

        try
        {
            var messages = await kafkaReader.ReadFromKafkaAsync(sourceKafka, batchSize: 50);

            if (!messages.Any())
            {
                logger.LogWarning("No messages found in source topic. Will retry later.");
                return;
            }       

            await kafkaWriter.WriteToKafkaAsync(targetKafka, messages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}