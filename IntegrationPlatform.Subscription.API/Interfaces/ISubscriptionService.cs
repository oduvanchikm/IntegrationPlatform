using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Subscription.API.Interfaces;

public interface ISubscriptionService
{
    Task<ConnectionResult> CreateOrchestrationConfigAsync(ConnectionRequest request);
    Task<List<OrchestrationConfig>> GetAllConnectionsAsync();
    Task<ConnectionResult> DeleteConnectionAsync(int orchestrationConfigId);
    Task<List<object>> GetAllInterfacesAsync();
    Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id);
}