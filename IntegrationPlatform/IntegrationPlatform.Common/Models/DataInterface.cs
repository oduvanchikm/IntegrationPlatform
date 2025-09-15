using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Common.Models;

public abstract class DataInterface
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public InterfaceType InterfaceType { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Draft;
    
    public int ProductId { get; set; }
    public Product Product { get; set; }
    
    public abstract object GetConnectionDetails();
}