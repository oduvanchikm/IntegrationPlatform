using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class KafkaReader(ILogger<KafkaReader> logger)
{
    private readonly ILogger<KafkaReader> _logger = logger;
    
    public async Task<List<string>> ReadFromKafkaAsync(KafkaInterface kafkaInterface, int batchSize = 100, int timeoutSeconds = 30)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaInterface.BootstrapServers,
            GroupId = $"kafka-to-kafka-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 30000,
            MaxPollIntervalMs = 300000
        };

        var message = new List<string>();

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(kafkaInterface.TopicName);

        _logger.LogInformation($"Consumer subscribed to topic {kafkaInterface.TopicName}");

        try
        {
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (message.Count < batchSize && (DateTime.UtcNow - startTime) < timeout)
            {
                var consumeResult = consumer.Consume(timeout);

                if (consumeResult == null) continue;

                message.Add(consumeResult.Message.Value);
                _logger.LogDebug("Consumed message from offset: {Offset}", consumeResult.Offset);

                consumer.Commit(consumeResult);
            }

            _logger.LogInformation("Consumed {Count} messages from {Topic}", message.Count, kafkaInterface.TopicName);
        }
        catch (ConsumeException e)
        {
            _logger.LogError($"Error occured: {e.Error.Reason}");
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            consumer.Close();
        }

        return message;
    }
}