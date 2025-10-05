using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(ILogger logger)
{
    private readonly ILogger _logger = logger;

    public async Task ExecuteAsync(DataInterface pub, DataInterface sub)
    {
        _logger.LogInformation("ApiToKafkaHandler.ExecuteAsync");
    }
}