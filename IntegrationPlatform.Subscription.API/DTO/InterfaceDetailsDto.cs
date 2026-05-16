namespace IntegrationPlatform.Subscription.API.DTO;

public class InterfaceDetailsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public int InterfaceTypeCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    // API поля
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? Endpoint { get; set; }
    public string? Token { get; set; }

    // Kafka поля
    public string? BootstrapServers { get; set; }
    public string? TopicName { get; set; }

    // Database поля
    public string? DatabaseName { get; set; }
    public string? Scheme { get; set; }

    // Общие поля
    public string? Username { get; set; }
}