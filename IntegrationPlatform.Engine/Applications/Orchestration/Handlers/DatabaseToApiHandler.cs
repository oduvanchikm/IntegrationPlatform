using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using NCrontab;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class DatabaseToApiHandler(
    ILogger<DatabaseToApiHandler> logger,
    DatabaseReader databaseReader,
    ApiWriter apiWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
    private async Task TransferData(DatabaseInterface source, ApiInterface target)
    {
        try
        {
            logger.LogInformation("Starting transfer from {Source} to {Target}",
                source.DatabaseName, target.Endpoint);

            var totalRecords = 0;
            var batchCount = 0;

            await databaseReader.ReadInBatchesAsync(source, async (batch) =>
            {
                batchCount++;
                totalRecords += batch.Count;
                logger.LogInformation("Processing batch {BatchCount}: {Count} records", batchCount, batch.Count);

                await apiWriter.WriteToApiAsync(target, batch);
                logger.LogDebug("Batch {BatchCount} written to API", batchCount);
            });

            logger.LogInformation("Completed transfer: {TotalRecords} records in {BatchCount} batches",
                totalRecords, batchCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during transfer");
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
        logger.LogInformation("========== DATABASE TO API HANDLER ==========");

        var sourceDb = publicationInterface as DatabaseInterface;
        var targetApi = subscriptionInterface as ApiInterface;

        if (sourceDb == null || targetApi == null)
        {
            logger.LogError("Source is not Database or Target is not API");
            return;
        }

        logger.LogInformation("Source DB: {Host}:{Port}/{Database}.{Scheme}",
            sourceDb.Host, sourceDb.Port, sourceDb.DatabaseName, sourceDb.Scheme);
        logger.LogInformation("Target API: {Host}:{Port}{Endpoint}",
            targetApi.Host, targetApi.Port, targetApi.Endpoint);

        if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
        {
            await TransferData(sourceDb, targetApi);
            return;
        }

        await RunScheduledAsync(sourceDb.Id, targetApi.Id, scheduleCron, async (cancellationToken) =>
        {
            if (!await IsConnectionStillActive(sourceDb.Id, targetApi.Id))
            {
                logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                    sourceDb.Id, targetApi.Id);
                throw new OperationCanceledException();
            }

            await TransferData(sourceDb, targetApi);
        });
    }
}