using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(ILogger<KafkaConsumer> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly ILogger<KafkaConsumer> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

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
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    
                    if (consumeResult?.Message?.Value == null)
                        continue;

                    _logger.LogInformation("Message received from offset: {Offset}", consumeResult.Offset);

                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<KafkaMessageHandler>();
                    
                    await handler.HandleMessageAsync(consumeResult.Message.Value);
                    
                    consumer.StoreOffset(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing message");
                }
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