using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.API.Metrics;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
    : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> CreateConnection([FromBody] ConnectionRequest request)
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();
        try
        {
            var result = await subscriptionService.CreateOrchestrationConfigAsync(request);

            if (result.Success)
            {
                SubscriptionMetrics.SubscriptionsCreated.Inc();
                SubscriptionMetrics.ActiveSubscriptions.Inc();
            }
            else
            {
                SubscriptionMetrics.ErrorsTotal.WithLabels("create_connection_failed").Inc();
                return BadRequest(new { result.Success, result.Error });
            }

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
            SubscriptionMetrics.ErrorsTotal.WithLabels("create_connection_exception").Inc();
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();
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

            ConsumerInterface = c.DataInterface == null
                ? null
                : new
                {
                    c.DataInterface.Id,
                    c.DataInterface.Name,
                    c.DataInterface.Description,
                    c.DataInterface.InterfaceType,
                    c.DataInterface.Status,
                    ProductName = (c.DataInterface.Product != null) ? c.DataInterface.Product.NameProduct : null,

                    BootstrapServers = (c.DataInterface as KafkaInterface)?.BootstrapServers,
                    TopicName = (c.DataInterface as KafkaInterface)?.TopicName,

                    Host = (c.DataInterface as ApiInterface)?.Host,
                    Endpoint = (c.DataInterface as ApiInterface)?.Endpoint,

                    DatabaseName = (c.DataInterface as DatabaseInterface)?.DatabaseName,
                    Scheme = (c.DataInterface as DatabaseInterface)?.Scheme
                }
        });

        return Ok(result);
    }

    [HttpDelete("connections/{orchestrationConfigId}")]
    public async Task<IActionResult> DeleteConnection(int orchestrationConfigId)
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();
        var result = await subscriptionService.DeleteConnectionAsync(orchestrationConfigId);
        if (result.Success)
        {
            SubscriptionMetrics.ActiveSubscriptions.Dec();
            SubscriptionMetrics.SubscriptionsDeleted.Inc();
        }
        else
        {
            SubscriptionMetrics.ErrorsTotal.WithLabels("delete_connection_failed").Inc();
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("health-db")]
    public async Task<IActionResult> HealthDb()
    {
        var isDatabaseHealthy = await subscriptionService.CheckDatabaseHealthAsync();

        if (!isDatabaseHealthy)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "disconnected",
                timestamp = DateTime.UtcNow,
                service = "subscription-api"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow,
            service = "subscription-api"
        });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "subscription-api"
        });
    }
}