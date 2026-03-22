using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class KafkaWriter(ILogger logger)
{
    private readonly ILogger _logger = logger;
    
    public async Task WriteKafkaAsync(KafkaInterface targetKafka, List<string> kafkaMessages)
    {
        if (!kafkaMessages.Any())
        {
            _logger.LogInformation("No messages to send to target Kafka");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = targetKafka.BootstrapServers,
            SaslUsername = targetKafka.Username,
            SaslPassword = targetKafka.Password,
            LingerMs = 5,
            BatchSize = 16384
        };

        using var producer = new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka producer error: {Code} - {Reason}", error.Code, error.Reason);
            })
            .Build();

        var successCount = 0;
        var failCount = 0;

        try
        {
            foreach (var message in kafkaMessages)
            {
                try
                {
                    var kafkaMessage = new Message<Null, string>
                    {
                        Value = message
                    };

                    var deliveryResult = await producer.ProduceAsync(targetKafka.TopicName, kafkaMessage);
                    successCount++;

                    _logger.LogDebug("Message sent to target Kafka topic {Topic} at offset {Offset}",
                        targetKafka.TopicName, deliveryResult.Offset);
                }
                catch (ProduceException<Null, string> ex)
                {
                    failCount++;
                    _logger.LogError(ex, "Failed to send message to target Kafka: {Error}", ex.Error.Reason);
                }
            }

            _logger.LogInformation(
                "Successfully sent {SuccessCount}/{TotalCount} messages to target Kafka topic: {Topic}",
                successCount, kafkaMessages.Count, targetKafka.TopicName);

            if (failCount > 0)
            {
                _logger.LogWarning("Failed to send {FailCount} messages", failCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending to target Kafka");
            throw;
        }
    }
}