using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger logger, ApiReader apiReader, KafkaWriter kafkaWriter)
{
    private readonly ILogger _logger = logger;
    private readonly ApiReader _apiReader = apiReader;
    private readonly KafkaWriter _kafkaWriter = kafkaWriter;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("========== API TO KAFKA HANDLER EXECUTE START ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceApi == null || targetKafka == null)
        {
            _logger.LogError("Invalid source API or target kafka");
            return;
        }

        _logger.LogInformation("Source API: Endpoint={Endpoint}, Host={Host}, Port={Port}",
            sourceApi.Endpoint, sourceApi.Host, sourceApi.Port);
        _logger.LogInformation("Target Kafka: BootstrapServers={BS}, Topic={Topic}",
            targetKafka.BootstrapServers, targetKafka.TopicName);

        try
        {
            var data = await _apiReader.ReadFromApiAsync(sourceApi);

            if (!string.IsNullOrEmpty(data))
            {
                await _kafkaWriter.WriteKafkaAsync(targetKafka, new List<string> { data });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}