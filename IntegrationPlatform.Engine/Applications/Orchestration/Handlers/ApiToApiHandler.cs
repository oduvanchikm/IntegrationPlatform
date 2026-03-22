using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToApiHandler(ILogger<ApiToApiHandler> logger, ApiReader apiReader, ApiWriter apiWriter)
{
    private readonly ILogger<ApiToApiHandler> _logger = logger;
    private readonly ApiReader _apiReader = apiReader;
    private readonly ApiWriter _apiWriter = apiWriter;

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("========== API TO API ==========");
        var data = await _apiReader.ReadFromApiAsync(publicationInterface);
        if (!string.IsNullOrEmpty(data))
        {
            await _apiWriter.WriteToApiAsync(subscriptionInterface, new List<string> { data });
        }
    }
}