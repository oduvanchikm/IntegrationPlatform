using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Search.API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Search.API.Controllers;

[ApiController]
[Route("api/[controller]/interfaces")]
public class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet("by-product")]
    public async Task<IActionResult> GetInterfacesByProduct([FromQuery] string? productName)
    {
        var results = await searchService.SearchInterfacesByNameProductAsync(productName);
        return Ok(results);
    }

    [HttpGet("by-interface")]
    public async Task<IActionResult> GetProductByInterface([FromQuery] string? interfaceName)
    {
        var results = await searchService.SearchProductsByInterfacesAsync(interfaceName);
        return Ok(results);
    }

    [HttpGet("by-type")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] InterfaceType? interfaceType)
    {
        var results = await searchService.SearchInterfacesByTypeAsync(interfaceType);
        return Ok(results);
    }

    [HttpGet("advanced")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] string? productName,
        [FromQuery] string? interfaceName,
        [FromQuery] InterfaceType? interfaceType)
    {
        var results = await searchService.SearchInterfacesAdvanced(productName,
            interfaceName, interfaceType);
        return Ok(results);
    }

    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        var result = await searchService.GetInterfaceByIdAsync(id);
        if (result == null)
            return NotFound(new { Success = false, Error = $"Interface with ID {id} not found" });

        return Ok(result);
    }
    
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "search-api"
        });
    }
}