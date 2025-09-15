using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Publication.API.Models;

public class ApiInterface : DataInterface
{
    public string Host { get; set; }
    public string Port { get; set; }
    public string Endpoint { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Token { get; set; }
    public override object GetConnectionDetails() => this;
}