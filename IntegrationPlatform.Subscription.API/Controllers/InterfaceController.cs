using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.API.Metrics;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterfaceController(IInterfaceService interfaceService, ILogger<InterfaceController> logger)
    : ControllerBase
{
    [HttpGet("interfaces/{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();
        try
        {
            var interfaceNew = await interfaceService.GetInterfaceByIdAsync(id);

            if (interfaceNew == null)
            {
                SubscriptionMetrics.ErrorsTotal.WithLabels("interface_not_found").Inc();
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
            SubscriptionMetrics.ErrorsTotal.WithLabels("get_interface_by_id").Inc();
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve interface",
                details = ex.Message
            });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetAllInterfaces()
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();

        try
        {
            var interfaces = await interfaceService.GetAllInterfacesAsync();

            if (interfaces == null)
            {
                SubscriptionMetrics.ErrorsTotal.WithLabels("interface_not_found").Inc();
                return NotFound(new
                {
                    success = false,
                    error = $"Interfaces not found in subscription database"
                });
            }

            return Ok(interfaces);
        }
        catch (Exception ex)
        {
            SubscriptionMetrics.ErrorsTotal.WithLabels("get_interface_by_id").Inc();
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve interface",
                details = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateConsumerInterface([FromBody] CreateConsumerInterfaceRequest request)
    {
        using var timer = SubscriptionMetrics.RequestDuration.NewTimer();
        try
        {
            logger.LogInformation("Received consumer interface request for {InterfaceType}", request.InterfaceType);

            var result = await interfaceService.CreateInterfaceAsync(request);

            if (!result.Success)
            {
                SubscriptionMetrics.ErrorsTotal.WithLabels(result.ErrorType ?? "create_failed").Inc();
                return BadRequest(new { Success = false, Error = result.Error });
            }

            SubscriptionMetrics.InterfacesCreated.WithLabels(request.InterfaceType.ToString().ToLower()).Inc();
            SubscriptionMetrics.ActiveConsumerInterfaces.Inc();

            return Ok(new
            {
                Success = true,
                Message = "Consumer interface created successfully",
                InterfaceId = result.InterfaceId,
                ProductId = result.ProductId,
                InterfaceType = result.InterfaceType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating consumer interface");
            SubscriptionMetrics.ErrorsTotal.WithLabels("create_interface_exception").Inc();
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }
}