using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;
using IntegrationPlatform.Engine.Metrics;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class ApiToApiHandler(
    ILogger<ApiToApiHandler> logger,
    ApiReader apiReader,
    ApiWriter apiWriter,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext) : BaseBatchHandler(logger)
{
    private async Task TransferData(ApiInterface source, ApiInterface target)
    {
        try
        {
            logger.LogInformation("Starting transfer from API {Source} to API {Target}",
                source.Endpoint, target.Endpoint);

            var data = await apiReader.ReadFromApiAsync(source);

            if (!string.IsNullOrEmpty(data))
            {
                await apiWriter.WriteToApiAsync(target, new List<string> { data });
                logger.LogInformation("Successfully sent data to target API");
            }
            else
            {
                logger.LogWarning("No data received from source API");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API to API transfer");
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
        logger.LogInformation("========== API TO API HANDLER ==========");
        var pattern = "ApiToApi";
        using var timer = EngineMetrics.ProcessingDuration.WithLabels(pattern).NewTimer();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceApi = publicationInterface as ApiInterface;
            var targetApi = subscriptionInterface as ApiInterface;

            if (sourceApi == null || targetApi == null)
            {
                logger.LogError("Source or target is not API");
                return;
            }

            logger.LogInformation("Source API: {Host}:{Port}{Endpoint}",
                sourceApi.Host, sourceApi.Port, sourceApi.Endpoint);
            logger.LogInformation("Target API: {Host}:{Port}{Endpoint}",
                targetApi.Host, targetApi.Port, targetApi.Endpoint);

            if (string.IsNullOrEmpty(scheduleCron) || scheduleCron == "* * * * *")
            {
                await TransferData(sourceApi, targetApi);
                return;
            }

            await RunScheduledAsync(sourceApi.Id, targetApi.Id, scheduleCron, async (token) =>
            {
                token.ThrowIfCancellationRequested();

                if (!await IsConnectionStillActive(sourceApi.Id, targetApi.Id))
                {
                    logger.LogInformation("Connection {SourceId}→{TargetId} no longer exists",
                        sourceApi.Id, targetApi.Id);
                    throw new OperationCanceledException();
                }

                await TransferData(sourceApi, targetApi);
            }, cancellationToken);

            EngineMetrics.EventsProcessed.WithLabels(pattern).Inc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during API to API transfer");
            EngineMetrics.ProcessingErrors.WithLabels(pattern, "execution_error").Inc();
            throw;
        }
    }
}