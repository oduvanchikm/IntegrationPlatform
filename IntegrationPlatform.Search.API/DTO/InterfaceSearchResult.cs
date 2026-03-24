using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Search.API.DTO;

public class InterfaceSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public InterfaceType InterfaceType { get; set; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public string ProductName { get; set; }
    public int ProductId { get; set; }
    
    public string? Host { get; set; }
    public string? Port { get; set; }
    public string? Endpoint { get; set; }
    public string? Token { get; set; }
    
    public string? BootstrapServers { get; set; }
    public string? TopicName { get; set; }
    
    public string? DatabaseName { get; set; }
    public string? Scheme { get; set; }
}