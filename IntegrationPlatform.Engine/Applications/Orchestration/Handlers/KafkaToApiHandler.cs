using System.Text;
using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToApiHandler(ILogger<KafkaToApiHandler> logger, KafkaReader kafkaReader, ApiWriter apiWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== KAFKA TO API HANDLER EXECUTE START ==========");

        var sourceKafka = publicationInterface as KafkaInterface;
        var targetApi = subscriptionInterface as ApiInterface;

        if (sourceKafka == null || targetApi == null)
        {
            logger.LogError("Kafka and Kafka are required.");
            return;
        }

        logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
            sourceKafka.BootstrapServers, sourceKafka.TopicName); 
        logger.LogInformation("Target Kafka: Endpoint={Endpoint}, Host={Host}, Port={Port}",
            targetApi.Endpoint, targetApi.Host, targetApi.Port);

        try
        {
            var messages = await kafkaReader.ReadFromKafkaAsync(sourceKafka, batchSize: 50);

            if (!messages.Any())
            {
                logger.LogWarning("No messages found in source topic. Will retry later.");
                return;
            }

            await apiWriter.WriteToApiAsync(targetApi, messages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}