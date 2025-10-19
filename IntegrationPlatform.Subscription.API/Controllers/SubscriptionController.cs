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

            return Ok(new { 
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
        return Ok(connections);
    }

    [HttpDelete("connections/{orchestrationConfigId}")]
    public async Task<IActionResult> DeleteConnection(int orchestrationConfigId)
    {
        var result = await subscriptionService.DeleteConnectionAsync(orchestrationConfigId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}