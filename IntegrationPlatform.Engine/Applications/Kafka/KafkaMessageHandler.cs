using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Interfaces;
using IntegrationPlatform.Engine.Applications.Orchestration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public abstract class KafkaMessageHandler(
    OrchestrationService orchestrationService,
    ILogger<KafkaMessageHandler> logger) : IKafkaMessageHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaMessageHandler> _logger;

    public async Task HandleMessageAsync(string json)
    {
        _logger.LogInformation("Handling Kafka message");

        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            var payload = jsonDocument.RootElement.GetProperty("payload");
            var after = payload.GetProperty("after").ToString();

            var config = JsonSerializer.Deserialize<OrchestrationConfigModel>(after);
            if (config == null)
            {
                _logger.LogError("Failed to deserialize OrchestrationConfig from payload");
                return;
            }

            _logger.LogInformation("Processing new orchestration config: {ConfigId}", config.Id);

            using var scope = _serviceProvider.CreateScope();
            var orchestrationService = scope.ServiceProvider.GetRequiredService<OrchestrationService>();

            await orchestrationService.HandleNewOrchestration(config);

            _logger.LogInformation("Successfully processed orchestration config: {ConfigId}", config.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle Kafka message");
        }
    }
}