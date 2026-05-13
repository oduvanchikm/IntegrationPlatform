using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
    : ControllerBase
{
    private static readonly Counter SubscriptionsCreated = Prometheus.Metrics
        .CreateCounter("subscriptions_created_total", "Total subscriptions created");

    private static readonly Gauge ActiveSubscriptions = Prometheus.Metrics
        .CreateGauge("subscriptions_active_total", "Active subscriptions");

    private static readonly Counter SubscriptionsDeleted = Prometheus.Metrics
        .CreateCounter("subscriptions_deleted_total", "Total subscriptions deleted");

    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("subscription_request_duration_seconds",
            "Duration of subscription API requests",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1, 2, 5] });

    private static readonly Counter ErrorsTotal = Prometheus.Metrics
        .CreateCounter("subscription_errors_total",
            "Total number of errors in subscription controller",
            new CounterConfiguration { LabelNames = ["error_type"] });

    [HttpGet("interfaces")]
    public async Task<IActionResult> GetAllInterfaces()
    {
        using var timer = RequestDuration.NewTimer();
        try
        {
            var interfaces = await subscriptionService.GetAllInterfacesAsync();
            return Ok(interfaces);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all interfaces: {Message}", ex.Message);
            ErrorsTotal.WithLabels("get_all_interfaces").Inc();
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve interfaces",
                details = ex.Message
            });
        }
    }

    [HttpGet("interfaces/{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        using var timer = RequestDuration.NewTimer();
        try
        {
            var interfaceNew = await subscriptionService.GetInterfaceByIdAsync(id);

            if (interfaceNew == null)
            {
                ErrorsTotal.WithLabels("interface_not_found").Inc();
                return NotFound(new
                {
                    success = false,
                    error = $"Interface with ID {id} not found in subscription database"
                });
            }

            return Ok(interfaceNew);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting interface by ID {Id}", id);
            ErrorsTotal.WithLabels("get_interface_by_id").Inc();
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve interface",
                details = ex.Message
            });
        }
    }

    [HttpPost("connect")]
    public async Task<IActionResult> CreateConnection([FromBody] ConnectionRequest request)
    {
        using var timer = RequestDuration.NewTimer();
        try
        {
            var result = await subscriptionService.CreateOrchestrationConfigAsync(request);

            if (result.Success)
            {
                SubscriptionsCreated.Inc();
                ActiveSubscriptions.Inc();
            }
            else
            {
                ErrorsTotal.WithLabels("create_connection_failed").Inc();
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
            ErrorsTotal.WithLabels("create_connection_exception").Inc();
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        using var timer = RequestDuration.NewTimer();
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
        using var timer = RequestDuration.NewTimer();
        var result = await subscriptionService.DeleteConnectionAsync(orchestrationConfigId);
        if (result.Success)
        {
            ActiveSubscriptions.Dec();
            SubscriptionsDeleted.Inc();
        }
        else
        {
            ErrorsTotal.WithLabels("delete_connection_failed").Inc();
        }

        return result.Success ? Ok(result) : BadRequest(result);
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