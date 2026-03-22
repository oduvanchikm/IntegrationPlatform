using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class ApiReader(ILogger logger)
{
    private readonly HttpClient _httpClient = new();
    private readonly ILogger _logger = logger;

    public async Task<string> ReadFromApiAsync(ApiInterface apiInterface)
    {
        var url = $"{apiInterface.Host}:{apiInterface.Port}{apiInterface.Endpoint}";
        logger.LogInformation("Calling API: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling API: {Url}", url);
            throw;
        }
    }
}