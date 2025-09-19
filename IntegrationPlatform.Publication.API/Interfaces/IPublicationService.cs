using IntegrationPlatform.Publication.API.DTO;

namespace IntegrationPlatform.Publication.API.Interfaces;

public interface IPublicationService
{
    Task<PublicationResult> PublishInterfaceAsync(InterfacePublishRequest request);
}