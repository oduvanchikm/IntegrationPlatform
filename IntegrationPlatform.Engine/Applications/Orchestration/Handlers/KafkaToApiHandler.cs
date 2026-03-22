using System.Text;
using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToApiHandler(ILogger logger, KafkaReader kafkaReader, ApiWriter apiWriter)
{
    private readonly ILogger _logger = logger;
    private readonly KafkaReader _kafkaReader = kafkaReader;
    private readonly ApiWriter _apiWriter = apiWriter;


    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("========== KAFKA TO API HANDLER EXECUTE START ==========");

        var sourceKafka = publicationInterface as KafkaInterface;
        var targetApi = subscriptionInterface as ApiInterface;

        if (sourceKafka == null || targetApi == null)
        {
            _logger.LogError("Kafka and Kafka are required.");
            return;
        }

        _logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
            sourceKafka.BootstrapServers, sourceKafka.TopicName);
        _logger.LogInformation("Target Kafka: Endpoint={Endpoint}, Host={Host}, Port={Port}",
            targetApi.Endpoint, targetApi.Host, targetApi.Port);

        try
        {
            var messages = await _kafkaReader.ReadFromKafkaAsync(sourceKafka, batchSize: 50);

            if (!messages.Any())
            {
                _logger.LogWarning("No messages found in source topic. Will retry later.");
                return;
            }

            await _apiWriter.WriteToApiAsync(targetApi, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}