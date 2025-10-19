namespace IntegrationPlatform.Subscription.API.DTO;

public class ConnectionResult
{
    public bool Success { get; set; }
    public int OrchestrationConfigId { get; set; }
    public string Error { get; set; }
}