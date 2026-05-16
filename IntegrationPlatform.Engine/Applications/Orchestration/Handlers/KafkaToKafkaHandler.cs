using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Engine.Metrics;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToKafkaHandler(
    ILogger<KafkaToKafkaHandler> logger,
    KafkaReader kafkaReader,
    KafkaWriter kafkaWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
    private async Task StartStreaming(KafkaInterface source, KafkaInterface target, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting streaming from Kafka {SourceTopic} to Kafka {TargetTopic}",
            source.TopicName, target.TopicName);

        await kafkaReader.ReadFromKafkaAsync(source, async (message) =>
        {
            logger.LogInformation("MESSAGE RECEIVED from {Topic}: {Preview}",
                source.TopicName,
                message.Length > 100 ? message[..100] + "..." : message);

            logger.LogDebug("Received message from Kafka topic {Topic}", source.TopicName);

            try
            {
                await kafkaWriter.WriteToKafkaAsync(target, new List<string> { message });
                logger.LogInformation("Message forwarded to {Topic}", target.TopicName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to forward message to {Topic}", target.TopicName);
                throw;
            }
        }, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("========== KAFKA TO KAFKA HANDLER ==========");

        var pattern = "KafkaToKafka";
        using var timer = EngineMetrics.ProcessingDuration.WithLabels(pattern).NewTimer();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceKafka = publicationInterface as KafkaInterface;
            var targetKafka = subscriptionInterface as KafkaInterface;

            if (sourceKafka == null || targetKafka == null)
            {
                logger.LogError("Source or target is not KafkaInterface");
                return;
            }

            logger.LogInformation("Source Kafka: {BootstrapServers}, Topic: {Topic}",
                sourceKafka.BootstrapServers, sourceKafka.TopicName);
            logger.LogInformation("Target Kafka: {BootstrapServers}, Topic: {Topic}",
                targetKafka.BootstrapServers, targetKafka.TopicName);

            if (!await IsConnectionStillActive(sourceKafka.Id, targetKafka.Id))
            {
                logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists, stopping streaming",
                    sourceKafka.Id, targetKafka.Id);
                return;
            }

            await StartStreaming(sourceKafka, targetKafka, cancellationToken);
            EngineMetrics.EventsProcessed.WithLabels(pattern).Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while processing Kafka message");
            EngineMetrics.ProcessingErrors.WithLabels(pattern, "execution_error").Inc();
            throw;
        }
    }
}