using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class DatabaseToKafkaHandler(ILogger<DatabaseToKafkaHandler> logger)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
    }
}