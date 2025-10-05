using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToKafkaHandler(ILogger logger)
{
    private readonly ILogger _logger = logger;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface,
        OrchestrationConfigModel config)
    {
        _logger.LogInformation("KafkaToKafkaHandler.ExecuteAsync");
    }
}