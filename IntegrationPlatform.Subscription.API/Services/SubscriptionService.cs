using System.Text.Json;
using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.API.Services;

public class SubscriptionService(
    SubscriptionDbContext context,
    ILogger<SubscriptionService> logger,
    IHttpClientFactory httpClientFactory)
    : ISubscriptionService
{
    private readonly SubscriptionDbContext _context = context;
    private readonly ILogger<SubscriptionService> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<ConnectionResult> CreateOrchestrationConfigAsync(ConnectionRequest request)
    {
        try
        {
            _logger.LogInformation("Creating orchestration config for PubId: {PubId}, SubId: {SubId}",
                request.PublicationInterfaceId, request.SubscriptionInterfaceId);

            var subscriptionInterface = await _context.DataInterfaces
                .FirstOrDefaultAsync(di => di.Id == request.SubscriptionInterfaceId);

            if (subscriptionInterface == null)
            {
                _logger.LogWarning("Consumer interface {Id} not found in subscription DB",
                    request.SubscriptionInterfaceId);
                return new ConnectionResult
                {
                    Success = false,
                    Error =
                        $"Consumer interface {request.SubscriptionInterfaceId} not found in subscription database. Please create it first."
                };
            }

            var sourceInterface = await FetchSourceInterfaceFromSearchApi(request.PublicationInterfaceId);

            if (sourceInterface == null)
            {
                _logger.LogWarning("Source interface {Id} not found in publication DB",
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

            _context.OrchestrationConfigs.Add(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
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
            _logger.LogError(ex, "Failed to create orchestration config");
            return new ConnectionResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<SourceInterfaceDto?> FetchSourceInterfaceFromSearchApi(int interfaceId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("SearchApi");

            var response = await httpClient.GetAsync($"/api/Search/interfaces/by-id/{interfaceId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Search API returned {StatusCode} for source interface {Id}",
                    response.StatusCode, interfaceId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<SourceInterfaceDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null)
            {
                _logger.LogWarning("Failed to deserialize source interface {Id} from Search API", interfaceId);
                return null;
            }

            _logger.LogInformation("Successfully fetched source interface {Id} from Search API: {Name} ({Type})",
                dto.Id, dto.Name, dto.InterfaceType);

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching source interface {Id} from Search API", interfaceId);
            return null;
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
                return new ConnectionResult
                {
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