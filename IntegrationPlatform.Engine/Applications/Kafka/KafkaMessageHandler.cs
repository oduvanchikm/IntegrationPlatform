using System.Text.Json;
using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Applications.Orchestration;
using IntegrationPlatform.Engine.Metrics;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaMessageHandler(
    IServiceProvider serviceProvider,
    ILogger<KafkaMessageHandler> logger)
    : IKafkaMessageHandler
{
    private static readonly Counter MessagesProcessed = Prometheus.Metrics
        .CreateCounter("engine_messages_processed_total",
            "Total messages processed by Engine",
            new CounterConfiguration
            {
                LabelNames = new[] { "pattern" }
            });

    private static readonly Histogram ProcessingTime = Prometheus.Metrics
        .CreateHistogram("engine_processing_seconds",
            "Processing time in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "pattern" }
            });

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