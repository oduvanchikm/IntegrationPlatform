using IntegrationPlatform.Subscription.API.DTO;

namespace IntegrationPlatform.Subscription.API.Interfaces;

public interface IInterfaceService
{
    Task<CreateInterfaceResult> CreateInterfaceAsync(CreateConsumerInterfaceRequest request);
    Task<List<object>> GetAllInterfacesAsync();
    Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id);
}