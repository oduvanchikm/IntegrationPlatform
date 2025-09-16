using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Publication.API.DTO;

public class InterfacePublishRequest
{
    // Обязательные поля
    public string Name { get; set; }
    public string ProductName { get; set; }
    public InterfaceType InterfaceType { get; set; }
        
    // Опциональные поля
    public string Description { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Consumer;
        
    // API specific
    public string Host { get; set; }
    public string Port { get; set; }
    public string Endpoint { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }
        
    // Database specific
    public string DatabaseName { get; set; }
    public string Scheme { get; set; }
        
    // Kafka specific
    public string BootstrapServers { get; set; }
    public string TopicName { get; set; }
}