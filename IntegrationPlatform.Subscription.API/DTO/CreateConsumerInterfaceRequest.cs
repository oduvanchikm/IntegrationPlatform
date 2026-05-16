using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Subscription.API.DTO;

public class CreateConsumerInterfaceRequest
{
    public string Name { get; set; }
    public string ProductName { get; set; }
    public InterfaceType InterfaceType { get; set; }
    public string? Description { get; set; }

    // API specific fields
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? Endpoint { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }

    // Database specific fields
    public string? DatabaseName { get; set; }
    public string? Scheme { get; set; }

    // Kafka specific fields
    public string? BootstrapServers { get; set; }
    public string? TopicName { get; set; }
}