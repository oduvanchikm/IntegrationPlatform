namespace IntegrationPlatform.Publication.API.DTO;

public class PublicationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? InterfaceId { get; set; }
    public int? ProductId { get; set; }
    public string? InterfaceType { get; set; }
    public string? Error { get; set; }
}