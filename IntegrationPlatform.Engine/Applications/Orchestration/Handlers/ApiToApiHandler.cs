using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToApiHandler(ILogger<ApiToApiHandler> logger, ApiReader apiReader, ApiWriter apiWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== API TO API HANDLER EXECUTE START ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetApi = subscriptionInterface as ApiInterface;

        if (sourceApi == null || targetApi == null)
        {
            logger.LogError("api and api are required.");
            return;
        }

        logger.LogInformation("Source Kafka: Endpoint={Endpoint}, Port={Port}, Host={Host}",
            sourceApi.Endpoint, sourceApi.Port, sourceApi.Host);
        logger.LogInformation("Target Kafka: Endpoint={Endpoint}, Port={Port}, Host={Host}",
            targetApi.Endpoint, targetApi.Port, targetApi.Host);

        try
        {
            var data = await apiReader.ReadFromApiAsync(sourceApi);
            if (!string.IsNullOrEmpty(data))
            {
                await apiWriter.WriteToApiAsync(targetApi, new List<string> { data });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ApiToApiHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}