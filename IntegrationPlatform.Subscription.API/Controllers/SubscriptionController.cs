using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
    : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> CreateConnection([FromBody] ConnectionRequest request)
    {
        try
        {
            var result = await subscriptionService.CreateOrchestrationConfigAsync(request);

            if (!result.Success)
                return BadRequest(new { result.Success, result.Error });

            return Ok(new
            {
                result.Success,
                result.OrchestrationConfigId,
                Message = "Connection created. Engine will automatically start integration via CDC."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create connection");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var connections = await subscriptionService.GetAllConnectionsAsync();
        var result = connections.Select(c => new
        {
            c.Id,
            c.InterfaceSubscriptionId,
            c.InterfacePublicationId,
            c.IntegrationPattern,
            c.ScheduleCron,
            c.MaxRetryAttempts,
            c.RetryDelaySeconds,
            c.ExecutionTimeoutSeconds,
            c.CreatedAt,
            c.UpdatedAt,
        
            // Данные consumer интерфейса (из subscription DB)
            ConsumerInterface = c.DataInterface == null ? null : new
            {
                c.DataInterface.Id,
                c.DataInterface.Name,
                c.DataInterface.Description,
                c.DataInterface.InterfaceType,
                c.DataInterface.Status,
                ProductName = (c.DataInterface.Product != null) ? c.DataInterface.Product.NameProduct : null,
            
                // Kafka specific
                BootstrapServers = (c.DataInterface as KafkaInterface)?.BootstrapServers,
                TopicName = (c.DataInterface as KafkaInterface)?.TopicName,
            
                // API specific
                Host = (c.DataInterface as ApiInterface)?.Host,
                Endpoint = (c.DataInterface as ApiInterface)?.Endpoint,
            
                // Database specific
                DatabaseName = (c.DataInterface as DatabaseInterface)?.DatabaseName,
                Scheme = (c.DataInterface as DatabaseInterface)?.Scheme
            }
        });
    
        return Ok(result);
    }

    [HttpDelete("connections/{orchestrationConfigId}")]
    public async Task<IActionResult> DeleteConnection(int orchestrationConfigId)
    {
        var result = await subscriptionService.DeleteConnectionAsync(orchestrationConfigId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}