using System.Text.Json;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToKafkaHandler(
    ILogger<ApiToKafkaHandler> logger,
    ApiReader apiReader,
    KafkaWriter kafkaWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
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

            logger.LogWarning("No 'message' field found in API response, using full response");
            return jsonData;
        }
        catch (JsonException)
        {
            logger.LogWarning("API response is not valid JSON, sending raw data");
            return jsonData;
        }
    }

    private async Task TransferData(ApiInterface source, KafkaInterface target)
    {
        try
        {
            logger.LogInformation("Starting transfer from API {Endpoint} to Kafka topic {Topic}",
                source.Endpoint, target.TopicName);

            var data = await apiReader.ReadFromApiAsync(source);

            if (!string.IsNullOrEmpty(data))
            {
                var messageToSend = ExtractMessage(data);
                logger.LogInformation("Extracted message: {Message}", messageToSend);

                await kafkaWriter.WriteToKafkaAsync(target, new List<string> { messageToSend });
                logger.LogInformation("Successfully sent message to Kafka topic {Topic}", target.TopicName);
            }
            else
            {
                logger.LogWarning("No data received from API");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API to Kafka transfer");
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
        logger.LogInformation("========== API TO KAFKA HANDLER ==========");

        var sourceApi = publicationInterface as ApiInterface;
        var targetKafka = subscriptionInterface as KafkaInterface;

        if (sourceApi == null || targetKafka == null)
        {
            logger.LogError("Source is not API or Target is not Kafka");
            return;
        }

        logger.LogInformation("Source API: {Host}:{Port}{Endpoint}",
            sourceApi.Host, sourceApi.Port, sourceApi.Endpoint);
        logger.LogInformation("Target Kafka: {BootstrapServers}, Topic={Topic}",
            targetKafka.BootstrapServers, targetKafka.TopicName);

        if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
        {
            await TransferData(sourceApi, targetKafka);
            return;
        }

        await RunScheduledAsync(sourceApi.Id, targetKafka.Id, scheduleCron, async (cancellationToken) =>
        {
            if (!await IsConnectionStillActive(sourceApi.Id, targetKafka.Id))
            {
                logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                    sourceApi.Id, targetKafka.Id);
                throw new OperationCanceledException();
            }

            await TransferData(sourceApi, targetKafka);
        });
    }
}