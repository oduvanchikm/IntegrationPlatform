using IntegrationPlatform.Publication.DataAccess.DTO;

namespace IntegrationPlatform.Publication.API.Interfaces;

public interface IPublicationService
{
    Task<PublicationResult> PublishInterfaceAsync(InterfacePublishRequest request);
    Task<bool> CheckDatabaseHealthAsync();
}