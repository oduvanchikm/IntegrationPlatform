using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger<ApiToKafkaHandler> logger, ApiReader apiReader, KafkaWriter kafkaWriter)
{
    private string ExtractMessage(string jsonData)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonData);

            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? string.Empty;
                }
            }
        
            if (doc.RootElement.TryGetProperty("message", out var directMessage))
            {
                return directMessage.GetString() ?? string.Empty;
            }

            logger.LogWarning("No 'message' field found in API response, using full response");
            return jsonData;
        }
        catch (JsonException)
        {
            logger.LogWarning("API response is not valid JSON, sending raw data");
            return jsonData;
        }
    }

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
                logger.LogInformation("{data}", data);
                var messageToSend = ExtractMessage(data);

                if (!string.IsNullOrEmpty(messageToSend))
                {
                    await kafkaWriter.WriteToKafkaAsync(targetKafka, new List<string> { messageToSend });
                    logger.LogInformation("Successfully sent message to Kafka: {Message}", messageToSend);
                }
                else
                {
                    logger.LogWarning("No message extracted from API response");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}