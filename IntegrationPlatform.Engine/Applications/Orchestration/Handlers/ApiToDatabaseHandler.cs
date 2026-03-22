using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToDatabaseHandler(ILogger<ApiToDatabaseHandler> logger, ApiReader apiReader, DatabaseWriter databaseWriter)
{
    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        logger.LogInformation("========== API TO DATABASE HANDLER EXECUTE START ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetDatabase = subscriptionInterface as DatabaseInterface;

        if (sourceApi == null || targetDatabase == null)
        {
            logger.LogError("Kafka and Kafka are required.");
            return;
        }

        logger.LogInformation("Source API: Endpoint={Endpoint}, Port={Port}, Host={Host}",
            sourceApi.Endpoint, sourceApi.Port, sourceApi.Host);
        logger.LogInformation("Target Database: DatabaseName={DatabaseName}, Port={Port}, Host={Host}",
            targetDatabase.DatabaseName, targetDatabase.Port, targetDatabase.Host);
        
        try
        {
            var data = await apiReader.ReadFromApiAsync(sourceApi);

            if (!string.IsNullOrEmpty(data))
            {
                await databaseWriter.WriteToDatabaseAsync(targetDatabase, new List<string> { data });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in KafkaToKafkaHandler.ExecuteAsync: {Message}", ex.Message);
        }
    }
}