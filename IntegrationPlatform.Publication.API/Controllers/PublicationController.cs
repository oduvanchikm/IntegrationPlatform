using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.API.DatabaseConnection;
using IntegrationPlatform.Publication.API.DTO;
using IntegrationPlatform.Publication.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(ILogger<PublicationController> logger, PublicationDbContext publicationDbContext)
    : ControllerBase
{
    private readonly ILogger<PublicationController> _logger = logger;
    private readonly PublicationDbContext _publicationDbContext = publicationDbContext;

    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ProductName))
            {
                return BadRequest(new { Success = false, Error = "ProductName is required" });
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { Success = false, Error = "Interface Name is required" });
            }

            var product = await _publicationDbContext.Products
                .FirstOrDefaultAsync(p => p.NameProduct == request.ProductName);

            if (product == null)
            {
                product = new Product
                {
                    NameProduct = request.ProductName,
                    ProductType = request.ProductType
                };
                _publicationDbContext.Products.Add(product);
                await _publicationDbContext.SaveChangesAsync();
            }

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

            _publicationDbContext.DataInterfaces.Add(newInterface);
            await _publicationDbContext.SaveChangesAsync();

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
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }
}