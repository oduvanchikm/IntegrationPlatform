using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Common.Models;

public class Product
{
    public int Id { get; set; }
    public string NameProduct { get; set; }
    public ProductType ProductType { get; set; }

    public List<DataInterface> Interfaces { get; set; } = new List<DataInterface>();
}