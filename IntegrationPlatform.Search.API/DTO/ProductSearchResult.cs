using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Search.API.DTO;

public class ProductSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ProductType ProductType { get; set; }
    public List<InterfaceInfo> Interfaces { get; set; } = new();
}

public class InterfaceInfo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public InterfaceType InterfaceType { get; set; }
    public ConnectionStatus ConnectionStatus { get; set; }
}