using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(
    IPublicationService publicationService,
    IConfiguration configuration,
    ILogger<PublicationController> logger) : ControllerBase
{
    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        logger.LogInformation("Publishing interfaces for publication");
        var result = await publicationService.PublishInterfaceAsync(request);

        if (!result.Success)
        {
            return string.IsNullOrEmpty(result.Error)
                ? BadRequest(new { Success = false, Error = "Unknown error" })
                : BadRequest(new { Success = false, Error = result.Error });
        }

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

    [HttpGet("test-connection")]
    public IActionResult TestConnection()
    {
        var kafkaService = configuration["Kafka:BootstrapServers"];

        if (string.IsNullOrEmpty(kafkaService))
        {
            return BadRequest(new { Success = false, Error = "Missing KafkaBootstrapServers" });
        }

        return Ok(new
        {
            KafkaBootstrapServers = kafkaService,
            Timestamp = DateTime.Now,
            Status = "API is running"
        });
    }
}