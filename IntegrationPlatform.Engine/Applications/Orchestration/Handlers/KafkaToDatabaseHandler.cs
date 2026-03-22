using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToDatabaseHandler(ILogger logger, IServiceProvider serviceProvider)
{
    private readonly ILogger _logger = logger;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("KafkaToDatabaseHandler.ExecuteAsync");
    }
}