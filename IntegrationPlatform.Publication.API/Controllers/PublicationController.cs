using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.API.DatabaseConnection;
using IntegrationPlatform.Publication.API.DTO;
using IntegrationPlatform.Publication.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(ILogger<PublicationController> logger, PublicationDbContext publicationDbContext)
    : ControllerBase
{
    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        try
        {
            logger.LogInformation("Received interface publication request: {@Request}", request);

            if (string.IsNullOrEmpty(request.ProductName))
            {
                logger.LogWarning("ProductName is required");
                return BadRequest(new { Success = false, Error = "ProductName is required" });
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                logger.LogWarning("Interface Name is required");
                return BadRequest(new { Success = false, Error = "Interface Name is required" });
            }

            logger.LogDebug("Looking for product: {ProductName}", request.ProductName);
            var product = await publicationDbContext.Products
                .FirstOrDefaultAsync(p => p.NameProduct == request.ProductName);

            if (product == null)
            {
                logger.LogInformation("Creating new product: {ProductName}", request.ProductName);

                product = new Product
                {
                    NameProduct = request.ProductName,
                    ProductType = request.ProductType
                };
                publicationDbContext.Products.Add(product);
                await publicationDbContext.SaveChangesAsync();
                logger.LogDebug("Created product with ID: {ProductId}", product.Id);
            }

            logger.LogDebug("Creating interface of type: {InterfaceType}", request.InterfaceType);

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

            publicationDbContext.DataInterfaces.Add(newInterface);
            await publicationDbContext.SaveChangesAsync();
            logger.LogInformation("Interface published successfully. ID: {InterfaceId}, Type: {InterfaceType}",
                newInterface.Id, newInterface.InterfaceType);

            return Ok(new
            {
                Success = true,
                Message = "Interface published successfully",
                InterfaceId = newInterface.Id,
                ProductId = product.Id,
                InterfaceType = newInterface.InterfaceType.ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing interface: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }
}