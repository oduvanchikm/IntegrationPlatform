using System.Text.Json;
using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Applications.Orchestration;
using IntegrationPlatform.Engine.Metrics;

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
            var config = JsonSerializer.Deserialize<OrchestrationConfigModel>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config != null && config.Id > 0)
            {
                logger.LogInformation("SUCCESS: Deserialized OrchestrationConfig with ID: {ConfigId}", config.Id);

                using var scope = serviceProvider.CreateScope();
                var orchestrationService = scope.ServiceProvider.GetRequiredService<OrchestrationService>();

                await orchestrationService.HandleNewOrchestration(config);
                EngineMetrics.EventsProcessed.WithLabels(config.IntegrationPattern ?? "unknown").Inc();
                logger.LogInformation("SUCCESS: Completed processing config {ConfigId}", config.Id);
            }
            else
            {
                logger.LogError("FAILED: Could not deserialize OrchestrationConfig");
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error: {Message}", ex.Message);
            EngineMetrics.ProcessingErrors.WithLabels("unknown", "json_parse_error").Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling Kafka message: {Message}", ex.Message);
            EngineMetrics.ProcessingErrors.WithLabels("unknown", "unexpected_error").Inc();
        }

        logger.LogInformation("========== KAFKA MESSAGE PROCESSING END ==========");
    }
}