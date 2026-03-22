using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Search.API.DTO;

namespace IntegrationPlatform.Search.API.Interfaces;

public interface ISearchService
{
    Task<List<InterfaceSearchResult>> SearchInterfacesByNameProductAsync(string? productName);
    Task<List<ProductSearchResult>> SearchProductsByInterfacesAsync(string? interfaceName);
    Task<List<InterfaceSearchResult>> SearchInterfacesByTypeAsync(InterfaceType? interfaceType);

    Task<List<InterfaceSearchResult>> SearchInterfacesAdvanced(string? productName, string? interfaceName,
        InterfaceType? interfaceType);

    Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id);
}