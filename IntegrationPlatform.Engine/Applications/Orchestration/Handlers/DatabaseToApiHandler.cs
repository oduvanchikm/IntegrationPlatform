using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class DatabaseToApiHandler(
    ILogger<DatabaseToApiHandler> logger,
    DatabaseReader databaseReader,
    ApiWriter apiWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
    }
}