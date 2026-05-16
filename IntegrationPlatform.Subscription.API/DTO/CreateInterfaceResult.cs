namespace IntegrationPlatform.Subscription.API.DTO;

public class CreateInterfaceResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorType { get; set; }
    public int? InterfaceId { get; set; }
    public int? ProductId { get; set; }
    public string? InterfaceType { get; set; }
}