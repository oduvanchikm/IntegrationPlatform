using System.Text.Json.Serialization;
using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Publication.DataAccess.DTO;

public class InterfacePublishRequest
{
    public string Name { get; set; }
    public string ProductName { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InterfaceType InterfaceType { get; set; }
        
    public string Description { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Consumer;
        
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? Endpoint { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
        
    public string? DatabaseName { get; set; }
    public string? Scheme { get; set; }
        
    public string? BootstrapServers { get; set; }
    public string? TopicName { get; set; }
}