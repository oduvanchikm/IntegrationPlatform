namespace IntegrationPlatform.IntegrationPlatform.Core.Models;

public class KafkaInterface
{
    public int Id { get; set; }
    public string BootstrapServer { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string TopicName { get; set; }
    
    public int ProductInterfaceId { get; set; }
    public ProductInterface ProductInterface { get; set; }
}