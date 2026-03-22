using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger<ApiToKafkaHandler> logger, ApiReader apiReader, KafkaWriter kafkaWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== API TO KAFKA HANDLER EXECUTE START ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceApi == null || targetKafka == null)
        {
            logger.LogError("Invalid source API or target kafka");
            return;
        }

        logger.LogInformation("Source API: Endpoint={Endpoint}, Host={Host}, Port={Port}",
            sourceApi.Endpoint, sourceApi.Host, sourceApi.Port);
        logger.LogInformation("Target Kafka: BootstrapServers={BS}, Topic={Topic}",
            targetKafka.BootstrapServers, targetKafka.TopicName);

        try
        {
            var data = await apiReader.ReadFromApiAsync(sourceApi);

            if (!string.IsNullOrEmpty(data))
            {
                await kafkaWriter.WriteToKafkaAsync(targetKafka, new List<string> { data });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}