using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterfaceController
{
    private readonly SubscriptionDbContext _context;
    private readonly ILogger<InterfaceController> _logger;

    public InterfaceController(SubscriptionDbContext context, ILogger<InterfaceController> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateConsumerInterface([FromBody] dynamic request)
    {
        try
        {
            _logger.LogInformation("Creating consumer interface");

            // Просто берем значения из JSON
            string name = request.GetProperty("name").GetString();
            string productName = request.GetProperty("productName").GetString();
            int interfaceTypeValue = request.GetProperty("interfaceType").GetInt32();
            var interfaceType = (InterfaceType)interfaceTypeValue;

            // Находим или создаем продукт
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.NameProduct == productName);
            
            if (product == null)
            {
                product = new Product
                {
                    NameProduct = productName,
                    ProductType = ProductType.Consumer
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
            }

            // Создаем интерфейс по типу
            DataInterface newInterface = interfaceType switch
            {
                InterfaceType.Kafka => new KafkaInterface
                {
                    Name = name,
                    Description = request.TryGetProperty("description", out var d) ? d.GetString() : "",
                    BootstrapServers = request.TryGetProperty("bootstrapServers", out var bs) ? bs.GetString() : "",
                    TopicName = request.TryGetProperty("topicName", out var tn) ? tn.GetString() : "",
                    Username = request.TryGetProperty("username", out var u) ? u.GetString() : "",
                    Password = request.TryGetProperty("password", out var p) ? p.GetString() : "",
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Kafka
                },
                
                InterfaceType.Api => new ApiInterface
                {
                    Name = name,
                    Description = request.TryGetProperty("description", out var d) ? d.GetString() : "",
                    Host = request.TryGetProperty("host", out var h) ? h.GetString() : "",
                    Port = request.TryGetProperty("port", out var p) ? p.GetString() : "",
                    Endpoint = request.TryGetProperty("endpoint", out var e) ? e.GetString() : "",
                    Username = request.TryGetProperty("username", out var u) ? u.GetString() : "",
                    Password = request.TryGetProperty("password", out var pw) ? pw.GetString() : "",
                    Token = request.TryGetProperty("token", out var t) ? t.GetString() : "",
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Api
                },
                
                InterfaceType.Db => new DatabaseInterface
                {
                    Name = name,
                    Description = request.TryGetProperty("description", out var d) ? d.GetString() : "",
                    Host = request.TryGetProperty("host", out var h) ? h.GetString() : "",
                    Port = request.TryGetProperty("port", out var p) ? p.GetString() : "",
                    Username = request.TryGetProperty("username", out var u) ? u.GetString() : "",
                    Password = request.TryGetProperty("password", out var pw) ? pw.GetString() : "",
                    DatabaseName = request.TryGetProperty("databaseName", out var db) ? db.GetString() : "",
                    Scheme = request.TryGetProperty("scheme", out var s) ? s.GetString() : "",
                    ProductId = product.Id,
                    Status = ConnectionStatus.Active,
                    InterfaceType = InterfaceType.Db
                },
                
                _ => throw new Exception($"Unknown type: {interfaceType}")
            };

            _context.DataInterfaces.Add(newInterface);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                InterfaceId = newInterface.Id,
                ProductId = product.Id,
                InterfaceType = newInterface.InterfaceType.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create consumer interface");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllInterfaces()
    {
        var interfaces = await _context.DataInterfaces
            .Include(di => di.Product)
            .Select(di => new
            {
                di.Id,
                di.Name,
                di.Description,
                di.InterfaceType,
                di.Status,
                ProductName = di.Product.NameProduct,
                
                // Kafka specific
                BootstrapServers = (di as KafkaInterface) != null ? ((KafkaInterface)di).BootstrapServers : null,
                TopicName = (di as KafkaInterface) != null ? ((KafkaInterface)di).TopicName : null,
                
                // API specific
                Host = (di as ApiInterface) != null ? ((ApiInterface)di).Host : null,
                Endpoint = (di as ApiInterface) != null ? ((ApiInterface)di).Endpoint : null,
                
                // Database specific
                DatabaseName = (di as DatabaseInterface) != null ? ((DatabaseInterface)di).DatabaseName : null,
                Scheme = (di as DatabaseInterface) != null ? ((DatabaseInterface)di).Scheme : null
            })
            .ToListAsync();
            
        return Ok(interfaces);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        var interface_ = await _context.DataInterfaces
            .Include(di => di.Product)
            .FirstOrDefaultAsync(di => di.Id == id);
            
        if (interface_ == null)
            return Not(new { Success = false, Error = $"Interface {id} not found" });
            
        return Ok(interface_);
    }
}