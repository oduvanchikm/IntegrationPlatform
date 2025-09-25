using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Publication.API.Services;

public class PublicationService(
    IDbContextFactory<PublicationDbContext> dbContextFactory,
    ILogger<PublicationService> logger)
    : IPublicationService
{
    private readonly IDbContextFactory<PublicationDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<PublicationService> _logger = logger;

    public async Task<PublicationResult> PublishInterfaceAsync(InterfacePublishRequest request)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            _logger.LogInformation("Received interface publication request: {@Request}", request);

            if (string.IsNullOrEmpty(request.ProductName))
            {
                _logger.LogWarning("ProductName is required");
                return new PublicationResult { Success = false, Error = "ProductName is required" };
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                _logger.LogWarning("Interface Name is required");
                return new PublicationResult { Success = false, Error = "Interface Name is required" };
            }

            _logger.LogDebug("Looking for product: {ProductName}", request.ProductName);
            var product = await context.Products
                .FirstOrDefaultAsync(p => p.NameProduct == request.ProductName);

            if (product == null)
            {
                _logger.LogInformation("Creating new product: {ProductName}", request.ProductName);

                product = new Product
                {
                    NameProduct = request.ProductName,
                    ProductType = request.ProductType
                };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                _logger.LogDebug("Created product with ID: {ProductId}", product.Id);
            }

            _logger.LogDebug("Creating interface of type: {InterfaceType}", request.InterfaceType);

            DataInterface newInterface = request.InterfaceType switch
            {
                InterfaceType.Api => new ApiInterface
                {
                    Name = request.Name,
                    Description = request.Description,
                    Host = request.Host,
                    Port = request.Port,
                    Endpoint = request.Endpoint,
                    Username = request.Username,
                    Password = request.Password,
                    Token = request.Token,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Api
                },
                InterfaceType.Db => new DatabaseInterface
                {
                    Name = request.Name,
                    Description = request.Description,
                    Host = request.Host,
                    Port = request.Port,
                    Username = request.Username,
                    Password = request.Password,
                    DatabaseName = request.DatabaseName,
                    Scheme = request.Scheme,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Db
                },
                InterfaceType.Kafka => new KafkaInterface
                {
                    Name = request.Name,
                    Description = request.Description,
                    BootstrapServers = request.BootstrapServers,
                    Username = request.Username,
                    Password = request.Password,
                    TopicName = request.TopicName,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Kafka
                },
                _ => throw new ArgumentException("Unsupported interface type")
            };

            context.DataInterfaces.Add(newInterface);
            await context.SaveChangesAsync();

            _logger.LogInformation("Interface published successfully. ID: {InterfaceId}, Type: {InterfaceType}",
                newInterface.Id, newInterface.InterfaceType);

            return new PublicationResult
            {
                Success = true,
                Message = "Interface published successfully",
                InterfaceId = newInterface.Id,
                ProductId = product.Id,
                InterfaceType = newInterface.InterfaceType.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing interface: {ErrorMessage}", ex.Message);
            return new PublicationResult { Success = false, Error = ex.Message };
        }
    }
}