using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IntegrationPlatform.Engine.Applications.Kafka;

public class KafkaConsumer(ILogger<KafkaConsumer> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Kafka Consumer is starting.");
        var kafkaSection = configuration.GetSection("Kafka");
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSection["BootstrapServers"],
            GroupId = kafkaSection["GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(kafkaSection["Topic"]);

        logger.LogInformation("Kafka consumer started. Listening topic {Topic}", kafkaSection["Topic"]);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                logger.LogInformation("Message received: {Message}", consumeResult.Message.Value);
                
                // todo вот тут по идее будет дергаться контроллер кубера крон джоб
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Kafka consumer failed.");
            throw;
        }
        finally
        {
            consumer.Close();
        }
    }
}