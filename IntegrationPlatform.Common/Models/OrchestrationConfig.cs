using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Common.Models;

public class OrchestrationConfig
{
    public int Id { get; set; }
    public int DataInterfaceId { get; set; }
    public DataInterface DataInterface { get; set; }
    
    public string ScheduleCron { get; set; } = "*/5 * * * *";
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 60;
    public int ExecutionTimeoutSeconds { get; set; } = 300;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}