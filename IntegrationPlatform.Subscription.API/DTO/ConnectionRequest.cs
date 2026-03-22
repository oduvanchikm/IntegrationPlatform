using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Subscription.API.DTO;

public class ConnectionRequest
{
    public int PublicationInterfaceId { get; set; }
    public int SubscriptionInterfaceId { get; set; }
    public IntegrationPattern IntegrationPattern { get; set; }

    public string ScheduleCron { get; set; } = "*/5 * * * *";
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 60;
    public int ExecutionTimeoutSeconds { get; set; } = 300;
}