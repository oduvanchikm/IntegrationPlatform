using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToApiHandler(ILogger<KafkaToApiHandler> logger, KafkaReader kafkaReader, ApiWriter apiWriter)
{
    private CancellationTokenSource _cts = new();
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
    
        await kafkaReader.ReadFromKafkaAsync(sourceKafka, async (message) =>
        {
            logger.LogInformation("Received message from Kafka, writing to database");
            await apiWriter.WriteToApiAsync(targetApi, new List<string> { message });
            logger.LogDebug("Message written to database");
        }, _cts.Token);
    }
    
    public void Stop()
    {
        _cts.Cancel();
    }
}