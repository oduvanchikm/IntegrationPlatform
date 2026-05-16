using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.API.DTO;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.API.Services;

public class InterfaceService(
    IDbContextFactory<SubscriptionDbContext> dbContextFactory,
    ILogger<InterfaceService> logger)
    : IInterfaceService
{
    public async Task<CreateInterfaceResult> CreateInterfaceAsync(CreateConsumerInterfaceRequest request)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();

            if (string.IsNullOrEmpty(request.ProductName))
            {
                return new CreateInterfaceResult
                {
                    Success = false,
                    Error = "ProductName is required",
                    ErrorType = "missing_product_name"
                };
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                return new CreateInterfaceResult
                {
                    Success = false,
                    Error = "Interface Name is required",
                    ErrorType = "missing_interface_name"
                };
            }

            logger.LogDebug("Looking for product: {ProductName}", request.ProductName);
            var product = await context.Products
                .FirstOrDefaultAsync(p => p.NameProduct == request.ProductName);

            if (product == null)
            {
                logger.LogInformation("Creating new consumer product: {ProductName}", request.ProductName);
                product = new Product
                {
                    NameProduct = request.ProductName,
                    ProductType = ProductType.Consumer
                };
                context.Products.Add(product);
                await context.SaveChangesAsync();
                logger.LogDebug("Created product with ID: {ProductId}", product.Id);
            }

            logger.LogDebug("Creating consumer interface of type: {InterfaceType}", request.InterfaceType);

            DataInterface newInterface = request.InterfaceType switch
            {
                InterfaceType.Api => new ApiInterface
                {
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    Host = request.Host ?? string.Empty,
                    Port = request.Port ?? string.Empty,
                    Endpoint = request.Endpoint ?? string.Empty,
                    Username = request.Username ?? string.Empty,
                    Password = request.Password ?? string.Empty,
                    Token = request.Token ?? string.Empty,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Api
                },

                InterfaceType.Db => new DatabaseInterface
                {
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    Host = request.Host ?? string.Empty,
                    Port = request.Port ?? string.Empty,
                    Username = request.Username ?? string.Empty,
                    Password = request.Password ?? string.Empty,
                    DatabaseName = request.DatabaseName ?? string.Empty,
                    Scheme = request.Scheme ?? string.Empty,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Db
                },

                InterfaceType.Kafka => new KafkaInterface
                {
                    Name = request.Name,
                    Description = request.Description ?? string.Empty,
                    BootstrapServers = request.BootstrapServers ?? string.Empty,
                    Username = request.Username ?? string.Empty,
                    Password = request.Password ?? string.Empty,
                    TopicName = request.TopicName ?? string.Empty,
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Kafka
                },

                _ => throw new ArgumentException($"Unsupported interface type: {request.InterfaceType}")
            };

            context.DataInterfaces.Add(newInterface);
            await context.SaveChangesAsync();

            logger.LogInformation("Consumer interface created successfully. ID: {InterfaceId}, Type: {InterfaceType}",
                newInterface.Id, newInterface.InterfaceType);

            return new CreateInterfaceResult
            {
                Success = true,
                InterfaceId = newInterface.Id,
                ProductId = product.Id,
                InterfaceType = newInterface.InterfaceType.ToString()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating consumer interface");
            return new CreateInterfaceResult
            {
                Success = false,
                Error = ex.Message,
                ErrorType = "exception"
            };
        }
    }

    public async Task<List<object>> GetAllInterfacesAsync()
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var interfaces = await context.DataInterfaces
                .Include(di => di.Product)
                .Select(di => new
                {
                    di.Id,
                    di.Name,
                    di.Description,
                    di.InterfaceType,
                    di.Status,
                    ProductName = di.Product != null ? di.Product.NameProduct : null,
                    ProductId = di.ProductId
                })
                .ToListAsync();

            return interfaces.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all interfaces");
            throw;
        }
    }

    public async Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            logger.LogInformation("Getting interface by ID {Id} from subscription database", id);

            var interfaceNew = await context.DataInterfaces
                .Include(di => di.Product)
                .FirstOrDefaultAsync(di => di.Id == id);

            if (interfaceNew == null)
            {
                logger.LogWarning("Interface with ID {Id} not found in subscription database", id);
                return null;
            }

            var dto = new InterfaceDetailsDto
            {
                Id = interfaceNew.Id,
                Name = interfaceNew.Name,
                Description = interfaceNew.Description ?? "",
                InterfaceType = interfaceNew.InterfaceType.ToString(),
                InterfaceTypeCode = (int)interfaceNew.InterfaceType,
                Status = interfaceNew.Status.ToString(),
                ProductId = interfaceNew.ProductId,
                ProductName = interfaceNew.Product?.NameProduct ?? "Unknown"
            };

            switch (interfaceNew)
            {
                case ApiInterface api:
                    dto.Host = api.Host;
                    dto.Port = api.Port;
                    dto.Endpoint = api.Endpoint;
                    dto.Token = api.Token;
                    dto.Username = api.Username;
                    break;

                case KafkaInterface kafka:
                    dto.BootstrapServers = kafka.BootstrapServers;
                    dto.TopicName = kafka.TopicName;
                    dto.Username = kafka.Username;
                    break;

                case DatabaseInterface db:
                    dto.Host = db.Host;
                    dto.Port = db.Port;
                    dto.DatabaseName = db.DatabaseName;
                    dto.Scheme = db.Scheme;
                    dto.Username = db.Username;
                    break;
            }

            return dto;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting interface by ID {Id}", id);
            throw;
        }
    }
}