using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class DatabaseToKafkaHandler(
    ILogger<DatabaseToKafkaHandler> logger,
    DatabaseReader databaseReader,
    KafkaWriter kafkaWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
    private async Task TransferData(DatabaseInterface source, KafkaInterface target)
    {
        try
        {
            logger.LogInformation("Starting transfer from database {Source} to Kafka topic {Topic}",
                source.DatabaseName, target.TopicName);

            var totalRecords = 0;
            var batchCount = 0;

            await databaseReader.ReadInBatchesAsync(source, async (batch) =>
            {
                batchCount++;
                totalRecords += batch.Count;
                logger.LogInformation("Processing batch {BatchCount}: {Count} records", batchCount, batch.Count);

                await kafkaWriter.WriteToKafkaAsync(target, batch);
                logger.LogDebug("Batch {BatchCount} sent to Kafka", batchCount);
            });

            logger.LogInformation(
                "Completed transfer: {TotalRecords} records in {BatchCount} batches to Kafka topic {Topic}",
                totalRecords, batchCount, target.TopicName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database to Kafka transfer");
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
        logger.LogInformation("========== DATABASE TO KAFKA HANDLER ==========");

        var sourceDb = publicationInterface as DatabaseInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceDb == null || targetKafka == null)
        {
            logger.LogError("Source is not Database or Target is not Kafka");
            return;
        }

        logger.LogInformation("Source DB: {Host}:{Port}/{Database}.{Scheme}",
            sourceDb.Host, sourceDb.Port, sourceDb.DatabaseName, sourceDb.Scheme);
        logger.LogInformation("Target Kafka: {BootstrapServers}, Topic={Topic}",
            targetKafka.BootstrapServers, targetKafka.TopicName);

        if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
        {
            await TransferData(sourceDb, targetKafka);
            return;
        }

        await RunScheduledAsync(sourceDb.Id, targetKafka.Id, scheduleCron, async (cancellationToken) =>
        {
            if (!await IsConnectionStillActive(sourceDb.Id, targetKafka.Id))
            {
                logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                    sourceDb.Id, targetKafka.Id);
                throw new OperationCanceledException();
            }

            await TransferData(sourceDb, targetKafka);
        });
    }
}