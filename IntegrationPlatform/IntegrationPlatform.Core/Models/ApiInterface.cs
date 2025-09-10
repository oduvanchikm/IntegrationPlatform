namespace IntegrationPlatform.IntegrationPlatform.Core.Models;

public class ApiInterface
{
    public int Id { get; set; }
    public string Host { get; set; }
    public string Port { get; set; }
    public string Endpoint { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }
    
    public int ProductInterfaceId { get; set; }
    public ProductInterface ProductInterface { get; set; }
}