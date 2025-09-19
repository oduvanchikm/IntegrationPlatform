using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Search.API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Search.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(ISearchService searchService) : ControllerBase
{
    private readonly ISearchService _searchService = searchService;

    [HttpGet("interfaces/by-product")]
    public async Task<IActionResult> GetInterfacesByProduct([FromQuery] string? productName)
    {
        var results = await _searchService.SearchInterfacesByNameProductAsync(productName);
        return Ok(results);
    }

    [HttpGet("interfaces/by-interface")]
    public async Task<IActionResult> GetProductByInterface([FromQuery] string? interfaceName)
    {
        var results = await _searchService.SearchProductsByInterfacesAsync(interfaceName);
        return Ok(results);
    }

    [HttpGet("interfaces/by-type")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] InterfaceType? interfaceType)
    {
        var results = await _searchService.SearchInterfacesByTypeAsync(interfaceType);
        return Ok(results);
    }

    [HttpGet("interfaces/advanced")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] string? productName,
        [FromQuery] string? interfaceName,
        [FromQuery] InterfaceType? interfaceType)
    {
        var results = await _searchService.SearchInterfacesAdvanced(productName, 
            interfaceName, interfaceType);
        return Ok(results);
    }
}