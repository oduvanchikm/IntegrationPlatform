using System.Text;
using System.Text.Json;
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

        while (true)
        {
            try
            {
                var messages = await kafkaReader.ReadFromKafkaAsync(sourceKafka, batchSize: 50);

                if (messages.Any())
                {
                    logger.LogInformation("Consumed {Count} messages from Kafka", messages.Count);
                    
                    var jsonMessages = messages.Select(msg => 
                        JsonSerializer.Serialize(new { message = msg, timestamp = DateTime.UtcNow })
                    ).ToList();
                    
                    await apiWriter.WriteToApiAsync(targetApi, jsonMessages);
                    logger.LogInformation("Successfully sent {Count} messages to API", messages.Count);
                }

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
                await Task.Delay(5000);
            }
        }
    }
}