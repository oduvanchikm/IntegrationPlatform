using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Search.API.Interfaces;
using IntegrationPlatform.Search.API.Metrics;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Search.API.Controllers;

[ApiController]
[Route("api/[controller]/interfaces")]
public class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet("by-product")]
    public async Task<IActionResult> GetInterfacesByProduct([FromQuery] string? productName)
    {
        SearchMetrics.SearchQueriesTotal.WithLabels("by_product").Inc();
        using var timer = SearchMetrics.SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesByNameProductAsync(productName);
        SearchMetrics.SearchResultsCount.Observe(results.Count);
        return Ok(results);
    }

    [HttpGet("by-interface")]
    public async Task<IActionResult> GetProductByInterface([FromQuery] string? interfaceName)
    {
        SearchMetrics.SearchQueriesTotal.WithLabels("by_interface").Inc();
        using var timer = SearchMetrics.SearchDuration.NewTimer();
        var results = await searchService.SearchProductsByInterfacesAsync(interfaceName);
        SearchMetrics.SearchResultsCount.Observe(results.Count);
        return Ok(results);
    }

    [HttpGet("by-type")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] InterfaceType? interfaceType)
    {
        SearchMetrics.SearchQueriesTotal.WithLabels("by_type").Inc();
        using var timer = SearchMetrics.SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesByTypeAsync(interfaceType);
        SearchMetrics.SearchResultsCount.Observe(results.Count);
        return Ok(results);
    }

    [HttpGet("advanced")]
    public async Task<IActionResult> GetInterfacesAdvanced([FromQuery] string? productName,
        [FromQuery] string? interfaceName,
        [FromQuery] InterfaceType? interfaceType)
    {
        SearchMetrics.SearchQueriesTotal.WithLabels("advanced").Inc();
        using var timer = SearchMetrics.SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesAdvanced(productName,
            interfaceName, interfaceType);
        SearchMetrics.SearchResultsCount.Observe(results.Count);
        return Ok(results);
    }

    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        SearchMetrics.SearchQueriesTotal.WithLabels("by_id").Inc();
        using var timer = SearchMetrics.SearchDuration.NewTimer();
        var result = await searchService.GetInterfaceByIdAsync(id);
        if (result == null)
            return NotFound(new { Success = false, Error = $"Interface with ID {id} not found" });
        return Ok(result);
    }

    [HttpGet("health-db")]
    public async Task<IActionResult> HealthDb()
    {
        var isDatabaseHealthy = await searchService.CheckDatabaseHealthAsync();

        if (!isDatabaseHealthy)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "disconnected",
                timestamp = DateTime.UtcNow,
                service = "search-api"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow,
            service = "search-api"
        });
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