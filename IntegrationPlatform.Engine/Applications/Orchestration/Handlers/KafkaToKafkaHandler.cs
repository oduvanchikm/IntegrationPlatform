using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToKafkaHandler(ILogger<KafkaToKafkaHandler> logger, KafkaReader kafkaReader, KafkaWriter kafkaWriter)
{
    private readonly ILogger _logger = logger;
    private readonly KafkaReader _kafkaReader = kafkaReader;
    private readonly KafkaWriter _kafkaWriter = kafkaWriter;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("========== KAFKA TO KAFKA HANDLER EXECUTE START ==========");

        var sourceKafka = publicationInterface as KafkaInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceKafka == null || targetKafka == null)
        {
            _logger.LogError("Kafka and Kafka are required.");
            return;
        }

        _logger.LogInformation("Source Kafka: BootstrapServers={BS}, Topic={Topic}",
                sourceKafka.BootstrapServers, sourceKafka.TopicName);
        _logger.LogInformation("Target Kafka: BootstrapServers={BS}, Topic={Topic}",
                targetKafka.BootstrapServers, targetKafka.TopicName);

        try
        {
            var messages = await _kafkaReader.ReadFromKafkaAsync(sourceKafka, batchSize: 50);

            if (!messages.Any())
            {
                _logger.LogWarning("No messages found in source topic. Will retry later.");
                return;
            }       

            await _kafkaWriter.WriteKafkaAsync(targetKafka, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}