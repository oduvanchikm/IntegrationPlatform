using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.API.Metrics;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(
    IPublicationService publicationService,
    ILogger<PublicationController> logger) : ControllerBase
{
    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("publication_request_duration_seconds",
            "Duration of publication API requests",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1, 2, 5] });

    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        using var timer = RequestDuration.NewTimer();
        logger.LogInformation("Publishing interfaces for publication");
        var result = await publicationService.PublishInterfaceAsync(request);

        if (!result.Success)
        {
            PublicationMetrics.ErrorsTotal.WithLabels("publish_failed").Inc();

            return string.IsNullOrEmpty(result.Error)
                ? BadRequest(new { Success = false, Error = "Unknown error" })
                : BadRequest(new { Success = false, Error = result.Error });
        }

        PublicationMetrics.InterfacesPublished.Inc();
        PublicationMetrics.ActiveSourceInterfaces.Inc();

        return Ok(new
        {
            result.Success,
            result.Message,
            result.InterfaceId,
            result.ProductId,
            result.InterfaceType
        });
    }

    [HttpGet("health-db")]
    public async Task<IActionResult> HealthDb()
    {
        var isDatabaseHealthy = await publicationService.CheckDatabaseHealthAsync();
        
        if (!isDatabaseHealthy)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "disconnected",
                timestamp = DateTime.UtcNow,
                service = "publication-api"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow,
            service = "publication-api"
        });
    }
    
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "publication-api"
        });
    }
}