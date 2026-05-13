using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(
    IPublicationService publicationService,
    IConfiguration configuration,
    ILogger<PublicationController> logger) : ControllerBase
{
    private static readonly Counter InterfacesPublished = Prometheus.Metrics
        .CreateCounter("publication_interfaces_published_total", 
            "Total number of interfaces published");

    private static readonly Gauge ActiveSourceInterfaces = Prometheus.Metrics
        .CreateGauge("publication_active_source_interfaces", 
            "Number of active source interfaces");

    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("publication_request_duration_seconds", 
            "Duration of publication API requests",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1, 2, 5] });

    private static readonly Counter ErrorsTotal = Prometheus.Metrics
        .CreateCounter("publication_errors_total", 
            "Total number of errors",
            new CounterConfiguration { LabelNames = ["error_type"] });
    
    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        using var timer = RequestDuration.NewTimer();
        logger.LogInformation("Publishing interfaces for publication");
        var result = await publicationService.PublishInterfaceAsync(request);

        if (!result.Success)
        {
            ErrorsTotal.WithLabels("publish_failed").Inc();
                
            return string.IsNullOrEmpty(result.Error)
                ? BadRequest(new { Success = false, Error = "Unknown error" })
                : BadRequest(new { Success = false, Error = result.Error });
        }

        InterfacesPublished.Inc();
        ActiveSourceInterfaces.Inc();

        return Ok(new
        {
            result.Success,
            result.Message,
            result.InterfaceId,
            result.ProductId,
            result.InterfaceType
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