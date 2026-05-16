using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Engine.Metrics;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class DatabaseToDatabaseHandler(
    ILogger<DatabaseToDatabaseHandler> logger,
    DatabaseReader databaseReader,
    DatabaseWriter databaseWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext)
    : BaseBatchHandler(logger)
{
    private async Task CopyDataInBatches(DatabaseInterface source, DatabaseInterface target)
    {
        try
        {
            logger.LogInformation("Starting batched copy from {Source} to {Target}",
                source.DatabaseName, target.DatabaseName);

            var totalRecords = 0;
            var batchCount = 0;

            await databaseReader.ReadInBatchesAsync(source, async (batch) =>
            {
                batchCount++;
                totalRecords += batch.Count;
                logger.LogInformation("Processing batch {BatchCount}: {Count} records", batchCount, batch.Count);

                await databaseWriter.WriteBatchAsync(target, batch);
                logger.LogDebug("Batch {BatchCount} written", batchCount);
            });

            logger.LogInformation("Completed copy: {TotalRecords} records in {BatchCount} batches",
                totalRecords, batchCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during batched copy");
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
        string scheduleCron, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("========== DATABASE TO DATABASE HANDLER ==========");
        var pattern = "DatabaseToDatabase";
        using var timer = EngineMetrics.ProcessingDuration.WithLabels(pattern).NewTimer();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceDb = publicationInterface as DatabaseInterface;
            var targetDb = subscriptionInterface as DatabaseInterface;

            if (sourceDb == null || targetDb == null)
            {
                logger.LogError("Source or target is not DatabaseInterface");
                return;
            }

            logger.LogInformation("Source DB: {Host}:{Port}/{Database}.{Scheme}",
                sourceDb.Host, sourceDb.Port, sourceDb.DatabaseName, sourceDb.Scheme);
            logger.LogInformation("Target DB: {Host}:{Port}/{Database}.{Scheme}",
                targetDb.Host, targetDb.Port, targetDb.DatabaseName, targetDb.Scheme);

            if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
            {
                await CopyDataInBatches(sourceDb, targetDb);
                return;
            }

            await RunScheduledAsync(sourceDb.Id, targetDb.Id, scheduleCron, async (token) =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!await IsConnectionStillActive(sourceDb.Id, targetDb.Id))
                    {
                        logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                            sourceDb.Id, targetDb.Id);
                        throw new OperationCanceledException();
                    }

                    await CopyDataInBatches(sourceDb, targetDb);
                }, cancellationToken
            );
            EngineMetrics.EventsProcessed.WithLabels(pattern).Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during running batched copy");
            EngineMetrics.ProcessingErrors.WithLabels(pattern, "execution_error").Inc();
            throw;
        }
    }
}