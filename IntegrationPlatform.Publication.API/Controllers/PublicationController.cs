using Confluent.Kafka;
using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.DataAccess.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationPlatform.Publication.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationController(
    IPublicationService publicationService,
    IConfiguration configuration,
    ILogger<PublicationController> logger) : ControllerBase
{
    private readonly IPublicationService _publicationService = publicationService;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<PublicationController> _logger = logger;

    [HttpPost("interfaces")]
    public async Task<IActionResult> PublishInterface([FromBody] InterfacePublishRequest request)
    {
        var result = await _publicationService.PublishInterfaceAsync(request);

        if (!result.Success)
        {
            return string.IsNullOrEmpty(result.Error)
                ? BadRequest(new { Success = false, Error = "Unknown error" })
                : BadRequest(new { Success = false, Error = result.Error });
        }

        return Ok(new
        {
            result.Success,
            result.Message,
            result.InterfaceId,
            result.ProductId,
            result.InterfaceType
        });
    }

    [HttpPost("test-and-save-kafka")]
    public async Task<IActionResult> TestAndSaveKafkaAsync(
        [FromQuery] string topic = "test-kafka",
        [FromQuery] string productName = "TestKafkaProduct")
    {
        var kafkaService = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrEmpty(kafkaService))
        {
            return BadRequest(new { Success = false, Error = "Missing KafkaBootstrapServers" });
        }

        var testResult = await TestKafkaConnectionAsync(kafkaService, topic);
        if (!testResult.Success)
        {
            return StatusCode(500, testResult);
        }

        var saveResult = await SaveKafkaInterfaceAsync(kafkaService, topic, productName);
        if (!saveResult.Success)
        {
            return StatusCode(500, saveResult);
        }

        return Ok(new
        {
            Success = true,
            Message = "Kafka connection tested and saved successfully",
            TestResult = testResult,
            SaveResult = saveResult
        });
    }

    private async Task<dynamic> TestKafkaConnectionAsync(string bootstrapServers, string topic)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            SocketTimeoutMs = 10000,
            MessageTimeoutMs = 10000,
            RequestTimeoutMs = 10000,
            EnableDeliveryReports = true
        };

        using var producer = new ProducerBuilder<Null, string>(config)
            .SetLogHandler((_, logMessage) =>
            {
                _logger.LogInformation("Kafka Log: {Facility} - {Message}", logMessage.Facility,
                    logMessage.Message);
            })
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka Error: {Code} - {Reason}", error.Code, error.Reason);
            })
            .Build();

        var messageValue = $"Test message from Publication API at {DateTime.Now}";

        try
        {
            var result = await producer.ProduceAsync(topic, new Message<Null, string> { Value = messageValue });
            _logger.LogInformation("Message sent to Kafka topic {Topic}, offset {Offset}", topic, result.Offset);

            return new
            {
                Success = true,
                Message = "Test message sent successfully",
                Topic = topic,
                KafkaServer = bootstrapServers,
                Offset = result.Offset.ToString(),
                TestTimestamp = DateTime.Now
            };
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(ex, "Kafka produce error: {ErrorReason}", ex.Error.Reason);
            return new
            {
                Success = false,
                Error = ex.Error.Reason,
                Code = ex.Error.Code.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test message to Kafka");
            return new
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<dynamic> SaveKafkaInterfaceAsync(string bootstrapServers, string topic, string productName)
    {
        try
        {
            var kafkaRequest = new InterfacePublishRequest
            {
                ProductName = productName,
                Name = $"Kafka Interface - {topic}",
                Description = $"Automatically created Kafka interface for topic {topic}",
                InterfaceType = Common.Enums.InterfaceType.Kafka,
                BootstrapServers = bootstrapServers,
                TopicName = topic,
                Host = new Uri(bootstrapServers).Host,
                Port = new Uri(bootstrapServers).Port.ToString(),
                Username = "default",
                Password = "2001"  
            };

            var result = await _publicationService.PublishInterfaceAsync(kafkaRequest);

            return new
            {
                result.Success,
                result.Message,
                result.InterfaceId,
                result.ProductId,
                result.InterfaceType,
                SaveTimestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Kafka interface to database");
            return new
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    [HttpPost("test-kafka")]
    public async Task<IActionResult> PublishTestKafkaAsync([FromQuery] string topic = "test-kafka")
    {
        var kafkaService = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrEmpty(kafkaService))
        {
            return BadRequest(new { Success = false, Error = "Missing KafkaBootstrapServers" });
        }

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaService,
            SocketTimeoutMs = 10000,
            MessageTimeoutMs = 10000,
            RequestTimeoutMs = 10000,
            EnableDeliveryReports = true
        };

        using var producer = new ProducerBuilder<Null, string>(config)
            .SetLogHandler((_, logMessage) =>
            {
                _logger.LogInformation("Kafka Log: {Facility} - {Message}", logMessage.Facility,
                    logMessage.Message);
            })
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka Error: {Code} - {Reason}", error.Code, error.Reason);
            })
            .Build();

        var messageValue = $"Test message from Publication API at {DateTime.Now}";

        try
        {
            var result = await producer.ProduceAsync(topic, new Message<Null, string> { Value = messageValue });
            _logger.LogInformation("Message sent to Kafka topic {Topic}, offset {Offset}", topic, result.Offset);

            return Ok(new
            {
                Success = true,
                Message = "Test message sent successfully",
                Topic = topic,
                KafkaServer = kafkaService,
                Offset = result.Offset.ToString()
            });
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError(ex, "Kafka produce error: {ErrorReason}", ex.Error.Reason);
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Error.Reason,
                Code = ex.Error.Code.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test message to Kafka");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("test-connection")]
    public IActionResult TestConnection()
    {
        var kafkaService = _configuration["Kafka:BootstrapServers"];

        if (string.IsNullOrEmpty(kafkaService))
        {
            return BadRequest(new { Success = false, Error = "Missing KafkaBootstrapServers" });
        }

        return Ok(new
        {
            KafkaBootstrapServers = kafkaService,
            Timestamp = DateTime.Now,
            Status = "API is running"
        });
    }
}