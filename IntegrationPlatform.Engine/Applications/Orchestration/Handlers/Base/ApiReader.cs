using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class ApiReader(ILogger<ApiReader> logger)
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> ReadFromApiAsync(ApiInterface apiInterface)
    {
        var url = $"{apiInterface.Host}:{apiInterface.Port}{apiInterface.Endpoint}";
        logger.LogInformation("Calling API: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            logger.LogInformation("API Response Status: {StatusCode}", response.StatusCode);
            logger.LogInformation("API Response Content: {Content}", content);

            response.EnsureSuccessStatusCode();
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling API: {Url}", url);
            throw;
        }
    }
}