using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.API.Services;

public class SubscriptionService(
    IDbContextFactory<SubscriptionDbContext> dbContextFactory,
    ILogger<SubscriptionService> logger,
    PublicationDbContext publicationContext)
    : ISubscriptionService
{
    public async Task<ConnectionResult> CreateOrchestrationConfigAsync(ConnectionRequest request)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();

            logger.LogInformation("Creating orchestration config for PubId: {PubId}, SubId: {SubId}",
                request.PublicationInterfaceId, request.SubscriptionInterfaceId);

            var subscriptionInterface = await context.DataInterfaces
                .FirstOrDefaultAsync(di => di.Id == request.SubscriptionInterfaceId);

            if (subscriptionInterface == null)
            {
                logger.LogWarning("Consumer interface {Id} not found in subscription DB",
                    request.SubscriptionInterfaceId);
                return new ConnectionResult
                {
                    Success = false,
                    Error =
                        $"Consumer interface {request.SubscriptionInterfaceId} not found in subscription database. Please create it first."
                };
            }

            var sourceInterface = await publicationContext.DataInterfaces
                .FirstOrDefaultAsync(di => di.Id == request.PublicationInterfaceId);

            if (sourceInterface == null)
            {
                logger.LogWarning("Source interface {Id} not found in publication DB",
                    request.PublicationInterfaceId);
                return new ConnectionResult
                {
                    Success = false,
                    Error = $"Source interface {request.PublicationInterfaceId} not found in publication database."
                };
            }

            var config = new OrchestrationConfig
            {
                InterfaceSubscriptionId = request.SubscriptionInterfaceId,
                InterfacePublicationId = request.PublicationInterfaceId,
                IntegrationPattern = request.IntegrationPattern,
                ScheduleCron = request.ScheduleCron,
                MaxRetryAttempts = request.MaxRetryAttempts,
                RetryDelaySeconds = request.RetryDelaySeconds,
                ExecutionTimeoutSeconds = request.ExecutionTimeoutSeconds,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.OrchestrationConfigs.Add(config);
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Created orchestration config {ConfigId} for {PubId} → {SubId}",
                config.Id, request.PublicationInterfaceId, request.SubscriptionInterfaceId);

            return new ConnectionResult
            {
                Success = true,
                OrchestrationConfigId = config.Id
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create orchestration config");
            return new ConnectionResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<OrchestrationConfig>> GetAllConnectionsAsync()
    {
        await using var context = await dbContextFactory.CreateDbContextAsync();
        return await context.OrchestrationConfigs
            .Include(oc => oc.DataInterface)
            .ToListAsync();
    }

    public async Task<ConnectionResult> DeleteConnectionAsync(int orchestrationConfigId)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var config = await context.OrchestrationConfigs
                .FirstOrDefaultAsync(oc => oc.Id == orchestrationConfigId);

            if (config == null)
            {
                return new ConnectionResult
                {
                    Success = false,
                    Error = "Connection not found"
                };
            }

            context.OrchestrationConfigs.Remove(config);
            await context.SaveChangesAsync();

            return new ConnectionResult { Success = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete connection {ConfigId}", orchestrationConfigId);
            return new ConnectionResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var canConnect = await context.Database.CanConnectAsync();
            return canConnect;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return false;
        }
    }
}