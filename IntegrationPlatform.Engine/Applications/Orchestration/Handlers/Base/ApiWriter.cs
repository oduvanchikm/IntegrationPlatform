using System.Text;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class ApiWriter(ILogger<ApiWriter> logger, IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ApiClient");

    public async Task WriteToApiAsync(ApiInterface apiInterface, List<string> messages)
    {
        if (!messages.Any())
        {
            logger.LogInformation("No messages to send to API");
            return;
        }

        var url = $"{apiInterface.Host}:{apiInterface.Port}{apiInterface.Endpoint}";
        var successCount = 0;
        var failCount = 0;

        foreach (var message in messages)
        {
            try
            {
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                successCount++;
                logger.LogDebug("Message sent to API: {Url}", url);
            }
            catch (Exception ex)
            {
                failCount++;
                logger.LogError(ex, "Failed to send message to API: {Url}", url);
            }
        }

        logger.LogInformation("Successfully sent {SuccessCount}/{TotalCount} messages to API",
            successCount, messages.Count);

        if (failCount > 0)
        {
            logger.LogWarning("Failed to send {FailCount} messages", failCount);
        }
    }
}