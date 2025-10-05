using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToDatabaseHandler(ILogger logger)
{
    private readonly ILogger _logger = logger;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface,
        OrchestrationConfigModel config)
    {
        _logger.LogInformation("KafkaToDatabaseHandler.ExecuteAsync");
    }
}