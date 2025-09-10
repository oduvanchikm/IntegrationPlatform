namespace IntegrationPlatform.IntegrationPlatform.Core.Models;

public class ProductInterface
{
    public int Id { get; set; }
    public string Specification { get; set; }
    public InterfaceType InterfaceType { get; set; }
    
    public int ProductId { get; set; }
    public Product Product { get; set; }
    
    public DatabaseInterface? DatabaseInterface { get; set; }
    public ApiInterface ApiInterface { get; set; }
    public KafkaInterface KafkaInterface { get; set; }
}