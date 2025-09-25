using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(IPublicationService publicationService) : ControllerBase
{
    private readonly IPublicationService _publicationService = publicationService;

    [HttpPost("interfaces")] 
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        var result = await _publicationService.PublishInterfaceAsync(request);

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
}