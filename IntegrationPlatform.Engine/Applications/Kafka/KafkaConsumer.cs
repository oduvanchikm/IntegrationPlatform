using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(ILogger<KafkaConsumer> logger, IConfiguration configuration, KafkaMessageHandler handler)
    : BackgroundService
{
    private readonly ILogger<KafkaConsumer> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly KafkaMessageHandler _handler = handler;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kafka Consumer is starting.");
        var kafkaSection = _configuration.GetSection("Kafka");
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSection["BootstrapServers"],
            GroupId = kafkaSection["GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(kafkaSection["Topic"]);

        _logger.LogInformation("Kafka consumer started. Listening topic {Topic}", kafkaSection["Topic"]);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                _logger.LogInformation("Message received: {Message}", consumeResult.Message.Value);

                await _handler.HandleMessageAsync(consumeResult.Message.Value);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Kafka consumer failed.");
            throw;
        }
        finally
        {
            consumer.Close();
        }
    }
}