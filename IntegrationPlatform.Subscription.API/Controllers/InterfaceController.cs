using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.API.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;

namespace IntegrationPlatform.Subscription.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterfaceController(SubscriptionDbContext context, ILogger<InterfaceController> logger)
    : ControllerBase
{
    private static readonly Counter InterfacesCreated = Prometheus.Metrics
        .CreateCounter("subscription_interfaces_created_total", 
            "Total number of consumer interfaces created",
            new CounterConfiguration { LabelNames = ["interface_type"] });
    
    private static readonly Gauge ActiveConsumerInterfaces = Prometheus.Metrics
        .CreateGauge("subscription_active_consumer_interfaces", 
            "Number of active consumer interfaces");
    
    private static readonly Counter ErrorsTotal = Prometheus.Metrics
        .CreateCounter("subscription_interface_errors_total", 
            "Total number of errors in interface controller",
            new CounterConfiguration { LabelNames = ["error_type"] });
    
    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("subscription_interface_request_duration_seconds", 
            "Duration of interface API requests",
            new HistogramConfiguration { Buckets = [0.01, 0.05, 0.1, 0.5, 1, 2, 5] });
    
    [HttpPost]
    public async Task<IActionResult> CreateConsumerInterface([FromBody] CreateConsumerInterfaceRequest request)
    {
        using var timer = RequestDuration.NewTimer();
        try
        {
            logger.LogInformation("Received consumer interface request: {@Request}", request);

            if (string.IsNullOrEmpty(request.ProductName))
            {
                logger.LogWarning("ProductName is required");
                ErrorsTotal.WithLabels("missing_product_name").Inc();
                return BadRequest(new { Success = false, Error = "ProductName is required" });
            }

            if (string.IsNullOrEmpty(request.Name))
            {
                logger.LogWarning("Interface Name is required");
                ErrorsTotal.WithLabels("missing_product_name").Inc();
                return BadRequest(new { Success = false, Error = "Interface Name is required" });
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
            
            string typeLabel = request.InterfaceType.ToString().ToLower();
            InterfacesCreated.WithLabels(typeLabel).Inc();
            
            ActiveConsumerInterfaces.Inc();

            logger.LogInformation("Consumer interface created successfully. ID: {InterfaceId}, Type: {InterfaceType}",
                newInterface.Id, newInterface.InterfaceType);

            return Ok(new
            {
                Success = true,
                Message = "Consumer interface created successfully",
                InterfaceId = newInterface.Id,
                ProductId = product.Id,
                InterfaceType = newInterface.InterfaceType.ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating consumer interface: {ErrorMessage}", ex.Message);
            ErrorsTotal.WithLabels("create_interface_exception").Inc();
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllInterfaces()
    {
        using var timer = RequestDuration.NewTimer();
        var interfaces = await context.DataInterfaces
            .Include(di => di.Product)
            .Select(di => new
            {
                di.Id,
                di.Name,
                di.Description,
                di.InterfaceType,
                di.Status,
                ProductName = di.Product.NameProduct,
                ProductType = di.Product.ProductType,

                BootstrapServers = (di as KafkaInterface) != null ? ((KafkaInterface)di).BootstrapServers : null,
                TopicName = (di as KafkaInterface) != null ? ((KafkaInterface)di).TopicName : null,

                Host = (di as ApiInterface) != null ? ((ApiInterface)di).Host : null,
                Port = (di as ApiInterface) != null ? ((ApiInterface)di).Port : null,
                Endpoint = (di as ApiInterface) != null ? ((ApiInterface)di).Endpoint : null,

                DatabaseName = (di as DatabaseInterface) != null ? ((DatabaseInterface)di).DatabaseName : null,
                Scheme = (di as DatabaseInterface) != null ? ((DatabaseInterface)di).Scheme : null
            })
            .ToListAsync();

        return Ok(interfaces);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInterfaceById(int id)
    {
        using var timer = RequestDuration.NewTimer();
        var interface_ = await context.DataInterfaces
            .Include(di => di.Product)
            .FirstOrDefaultAsync(di => di.Id == id);

        if (interface_ == null)
        {
            ErrorsTotal.WithLabels("interface_not_found").Inc();
            return NotFound(new { Success = false, Error = $"Consumer interface {id} not found" });
        }

        var result = new
        {
            interface_.Id,
            interface_.Name,
            interface_.Description,
            interface_.InterfaceType,
            interface_.Status,
            interface_.ProductId,
            ProductName = interface_.Product?.NameProduct,
            ProductType = interface_.Product?.ProductType,

            BootstrapServers = (interface_ as KafkaInterface)?.BootstrapServers,
            TopicName = (interface_ as KafkaInterface)?.TopicName,

            Host = (interface_ as ApiInterface)?.Host,
            Port = (interface_ as ApiInterface)?.Port,
            Endpoint = (interface_ as ApiInterface)?.Endpoint,
            Token = (interface_ as ApiInterface)?.Token,

            DatabaseName = (interface_ as DatabaseInterface)?.DatabaseName,
            Scheme = (interface_ as DatabaseInterface)?.Scheme,

            Username = (interface_ as ApiInterface)?.Username ??
                       (interface_ as DatabaseInterface)?.Username ??
                       (interface_ as KafkaInterface)?.Username,
            Password = (interface_ as ApiInterface)?.Password ??
                       (interface_ as DatabaseInterface)?.Password ??
                       (interface_ as KafkaInterface)?.Password
        };

        return Ok(result);
    }
}