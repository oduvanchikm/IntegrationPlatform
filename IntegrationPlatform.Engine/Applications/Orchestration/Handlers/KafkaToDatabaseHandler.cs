using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToDatabaseHandler(ILogger<KafkaToDatabaseHandler> logger, IServiceProvider serviceProvider)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("KafkaToDatabaseHandler.ExecuteAsync");
    }
}