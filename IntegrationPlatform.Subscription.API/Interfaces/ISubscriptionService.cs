using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;

namespace IntegrationPlatform.Subscription.API.Interfaces;

public interface ISubscriptionService
{
    Task<ConnectionResult> CreateOrchestrationConfigAsync(ConnectionRequest request);
    Task<List<OrchestrationConfig>> GetAllConnectionsAsync();
    Task<ConnectionResult> DeleteConnectionAsync(int orchestrationConfigId);
    Task<bool> CheckDatabaseHealthAsync();
}