using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using NCrontab;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToDatabaseHandler(
    ILogger<ApiToDatabaseHandler> logger,
    ApiReader apiReader,
    DatabaseWriter databaseWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
    private async Task TransferData(ApiInterface source, DatabaseInterface target)
    {
        try
        {
            logger.LogInformation("Starting transfer from API {Endpoint} to database {Database}",
                source.Endpoint, target.DatabaseName);

            var data = await apiReader.ReadFromApiAsync(source);

            if (!string.IsNullOrEmpty(data))
            {
                var messageToSave = ExtractMessage(data);
                logger.LogInformation("Extracted message: {Message}", messageToSave);

                await databaseWriter.WriteToDatabaseAsync(target, new List<string> { messageToSave });
                logger.LogInformation("Successfully saved message to database");
            }
            else
            {
                logger.LogWarning("No data received from API");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API to database transfer");
        }
    }

    private string ExtractMessage(string jsonData)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonData);

            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? string.Empty;
                }
            }

            if (doc.RootElement.TryGetProperty("message", out var directMessage))
            {
                return directMessage.GetString() ?? string.Empty;
            }

            logger.LogWarning("No 'message' field found, using full response");
            return jsonData;
        }
        catch (JsonException)
        {
            logger.LogWarning("API response is not valid JSON, sending raw data");
            return jsonData;
        }
    }

    private async Task<bool> IsConnectionStillActive(int sourceId, int targetId)
    {
        await using var publicationDb = await publicationDbContext.CreateDbContextAsync();
        await using var subscriptionDb = await subscriptionDbContext.CreateDbContextAsync();

        var sourceExists = await publicationDb.DataInterfaces.AnyAsync(d => d.Id == sourceId);
        var targetExists = await subscriptionDb.DataInterfaces.AnyAsync(d => d.Id == targetId);

        return sourceExists && targetExists;
    }

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface,
        string scheduleCron)
    {
        logger.LogInformation("========== API TO DATABASE HANDLER ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetDb = subscriptionInterface as DatabaseInterface;

        if (sourceApi == null || targetDb == null)
        {
            logger.LogError("Source is not API or Target is not Database");
            return;
        }

        logger.LogInformation("Source API: {Host}:{Port}{Endpoint}",
            sourceApi.Host, sourceApi.Port, sourceApi.Endpoint);
        logger.LogInformation("Target DB: {Host}:{Port}/{Database}.{Scheme}",
            targetDb.Host, targetDb.Port, targetDb.DatabaseName, targetDb.Scheme);

        if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
        {
            await TransferData(sourceApi, targetDb);
            return;
        }

        await RunScheduledAsync(sourceApi.Id, targetDb.Id, scheduleCron, async (cancellationToken) =>
        {
            if (!await IsConnectionStillActive(sourceApi.Id, targetDb.Id))
            {
                logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                    sourceApi.Id, targetDb.Id);
                throw new OperationCanceledException();
            }

            await TransferData(sourceApi, targetDb);
        });
    }
}