using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.API.Services;

public class SubscriptionService(SubscriptionDbContext context, ILogger<SubscriptionService> logger)
    : ISubscriptionService
{
    private readonly SubscriptionDbContext _context = context;
    private readonly ILogger<SubscriptionService> _logger = logger;

    public async Task<ConnectionResult> CreateOrchestrationConfigAsync(ConnectionRequest request)
    {
        try
        {
            var subscriptionInterface = await _context.DataInterfaces
                .FirstOrDefaultAsync(di => di.Id == request.SubscriptionInterfaceId);
                
            if (subscriptionInterface == null)
            {
                return new ConnectionResult { 
                    Success = false, 
                    Error = "Subscription interface not found" 
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

            _context.OrchestrationConfigs.Add(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created orchestration config {ConfigId} for {PubId} → {SubId}", 
                config.Id, request.PublicationInterfaceId, request.SubscriptionInterfaceId);

            return new ConnectionResult { 
                Success = true, 
                OrchestrationConfigId = config.Id 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create orchestration config");
            return new ConnectionResult { Success = false, Error = ex.Message };
        }
    }
    
    public async Task<List<OrchestrationConfig>> GetAllConnectionsAsync()
    {
        return await _context.OrchestrationConfigs
            .Include(oc => oc.DataInterface) // Subscription interface
            .ToListAsync();
    }

    public async Task<ConnectionResult> DeleteConnectionAsync(int orchestrationConfigId)
    {
        try
        {
            var config = await _context.OrchestrationConfigs
                .FirstOrDefaultAsync(oc => oc.Id == orchestrationConfigId);
                
            if (config == null)
            {
                return new ConnectionResult { 
                    Success = false, 
                    Error = "Connection not found" 
                };
            }

            _context.OrchestrationConfigs.Remove(config);
            await _context.SaveChangesAsync();

            return new ConnectionResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete connection {ConfigId}", orchestrationConfigId);
            return new ConnectionResult { Success = false, Error = ex.Message };
        }
    }
}