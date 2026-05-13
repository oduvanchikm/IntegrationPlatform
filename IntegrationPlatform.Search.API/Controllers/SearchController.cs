using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Search.API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace IntegrationPlatform.Search.API.Controllers;

[ApiController]
[Route("api/[controller]/interfaces")]
public class SearchController(ISearchService searchService) : ControllerBase
{
    private static readonly Counter SearchQueriesTotal = Prometheus.Metrics
        .CreateCounter("search_queries_total",
            "Total number of search queries",
            new CounterConfiguration { LabelNames = ["query_type"] });

    private static readonly Histogram SearchDuration = Prometheus.Metrics
        .CreateHistogram("search_duration_seconds",
            "Duration of search queries",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1] });

    public static readonly Histogram SearchResultsCount = Prometheus.Metrics
        .CreateHistogram("search_results_count",
            "Number of results returned by search");

    [HttpGet("by-product")]
    public async Task<IActionResult> GetInterfacesByProduct([FromQuery] string? productName)
    {
        SearchQueriesTotal.WithLabels("by_product").Inc();
        using var timer = SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesByNameProductAsync(productName);
        return Ok(results);
    }

    [HttpGet("by-interface")]
    public async Task<IActionResult> GetProductByInterface([FromQuery] string? interfaceName)
    {
        SearchQueriesTotal.WithLabels("by_interface").Inc();
        using var timer = SearchDuration.NewTimer();
        var results = await searchService.SearchProductsByInterfacesAsync(interfaceName);
        return Ok(results);
    }

    [HttpGet("by-type")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] InterfaceType? interfaceType)
    {
        SearchQueriesTotal.WithLabels("by_type").Inc();
        using var timer = SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesByTypeAsync(interfaceType);
        return Ok(results);
    }

    [HttpGet("advanced")]
    public async Task<IActionResult> GetInterfacesByType([FromQuery] string? productName,
        [FromQuery] string? interfaceName,
        [FromQuery] InterfaceType? interfaceType)
    {
        SearchQueriesTotal.WithLabels("advanced").Inc();
        using var timer = SearchDuration.NewTimer();
        var results = await searchService.SearchInterfacesAdvanced(productName,
            interfaceName, interfaceType);
        return Ok(results);
    }

    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        SearchQueriesTotal.WithLabels("by_id").Inc();
        using var timer = SearchDuration.NewTimer();
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