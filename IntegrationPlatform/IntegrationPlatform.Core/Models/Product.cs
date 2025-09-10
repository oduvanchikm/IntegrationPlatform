namespace IntegrationPlatform.IntegrationPlatform.Core.Models;

public class Product
{
    public int Id { get; set; }
    public string NameProduct { get; set; }
    public ProductType ProductType { get; set; }

    public List<ProductInterface> Interfaces { get; set; } = [];
}