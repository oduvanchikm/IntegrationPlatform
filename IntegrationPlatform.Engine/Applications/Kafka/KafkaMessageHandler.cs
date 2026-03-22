using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Applications.Orchestration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaMessageHandler(
    IServiceProvider serviceProvider,
    ILogger<KafkaMessageHandler> logger)
    : IKafkaMessageHandler
{
    public async Task HandleMessageAsync(string json)
    {
        logger.LogInformation("========== KAFKA MESSAGE RECEIVED ==========");
        logger.LogInformation("Raw message: {Json}", json);

        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            logger.LogInformation("Successfully parsed JSON");

            // Проверяем структуру сообщения Debezium
            if (jsonDocument.RootElement.TryGetProperty("payload", out var payload))
            {
                logger.LogInformation("Found 'payload' field");

                // Debezium отправляет { payload: { after: {...}, before: {...}, op: 'c' } }
                if (payload.TryGetProperty("after", out var after))
                {
                    logger.LogInformation("Found 'after' field: {After}", after.ToString());

                    // Получаем операцию (c = create, u = update, d = delete)
                    if (payload.TryGetProperty("op", out var op))
                    {
                        logger.LogInformation("Operation type: {Op}", op.GetString());
                    }

                    var config = JsonSerializer.Deserialize<OrchestrationConfigModel>(after.ToString());
                    if (config != null)
                    {
                        logger.LogInformation("SUCCESS: Deserialized OrchestrationConfig with ID: {ConfigId}",
                            config.Id);
                        logger.LogInformation("Config details: PubId={PubId}, SubId={SubId}, Pattern={Pattern}",
                            config.InterfacePublicationId,
                            config.InterfaceSubscriptionId,
                            config.IntegrationPattern);

                        using var scope = serviceProvider.CreateScope();
                        var orchestrationService = scope.ServiceProvider.GetRequiredService<OrchestrationService>();

                        logger.LogInformation(
                            "Calling OrchestrationService.HandleNewOrchestration for config {ConfigId}", config.Id);
                        await orchestrationService.HandleNewOrchestration(config);

                        logger.LogInformation("SUCCESS: Completed processing config {ConfigId}", config.Id);
                    }
                    else
                    {
                        logger.LogError("FAILED: Could not deserialize OrchestrationConfig from after field");
                    }
                }
                else
                {
                    logger.LogWarning("No 'after' field in payload. Available fields: {Fields}",
                        string.Join(", ", payload.EnumerateObject().Select(p => p.Name)));
                }
            }
            else
            {
                logger.LogWarning("No 'payload' field in message. Root fields: {Fields}",
                    string.Join(", ", jsonDocument.RootElement.EnumerateObject().Select(p => p.Name)));
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling Kafka message: {Message}", ex.Message);
        }

        logger.LogInformation("========== KAFKA MESSAGE PROCESSING END ==========");
    }
}