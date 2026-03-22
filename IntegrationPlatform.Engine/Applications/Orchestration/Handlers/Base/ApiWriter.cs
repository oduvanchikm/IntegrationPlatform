using System.Text;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class ApiWriter(ILogger logger)
{
    private readonly ILogger _logger = logger;
    private readonly HttpClient _httpClient = new();
    
    public async Task WriteToApiAsync(ApiInterface apiInterface, List<string> messages)
    {
        if (!messages.Any())
        {
            _logger.LogInformation("No messages to send to API");
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
                _logger.LogDebug("Message sent to API: {Url}", url);
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "Failed to send message to API: {Url}", url);
            }
        }

        _logger.LogInformation("Successfully sent {SuccessCount}/{TotalCount} messages to API",
            successCount, messages.Count);

        if (failCount > 0)
        {
            _logger.LogWarning("Failed to send {FailCount} messages", failCount);
        }
    }
}