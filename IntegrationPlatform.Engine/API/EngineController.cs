using IntegrationPlatform.Engine.Applications.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Engine.API;

[ApiController]
[Route("api/[controller]")]
public class EngineController : ControllerBase
{
    [HttpPost("stop-task")]
    public IActionResult StopTask([FromQuery] int sourceId, [FromQuery] int targetId, [FromQuery] string pattern)
    {
        OrchestrationService.StopScheduledTask(sourceId, targetId, pattern);

        return Ok(new
        {
            success = true,
            message = $"Task {sourceId}→{targetId} with pattern {pattern} stopped"
        });
    }
}