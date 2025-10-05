using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public abstract class KafkaMessageHandler(OrchestrationService orchestrationService, ILogger<KafkaMessageHandler> logger)
{
    private readonly OrchestrationService _orchestrationService = orchestrationService;
    private readonly ILogger<KafkaMessageHandler> _logger = logger;

    public async Task HandleMessageAsync(string json)
    {
        _logger.LogInformation("Handling message");
        var payload = JsonDocument.Parse(json).RootElement.GetProperty("payload");
        var after = payload.GetProperty("after").ToString();

        var config = JsonSerializer.Deserialize<OrchestrationConfigModel>(after);
        if (config == null)
        {
            _logger.LogError("Failed to deserialize payload");
            return;
        }

        _logger.LogInformation("Received message");
        await _orchestrationService.HandleNewOrchestration(config);
    }
}