using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Publication.API.Models;

public class KafkaInterface : DataInterface
{
    public string BootstrapServers { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string TopicName { get; set; }
    
    public override object GetConnectionDetails() => this;
}