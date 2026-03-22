using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class KafkaWriter(ILogger<KafkaWriter> logger)
{
    public async Task WriteToKafkaAsync(KafkaInterface targetKafka, List<string> kafkaMessages)
    {
        if (!kafkaMessages.Any())
        {
            logger.LogInformation("No messages to send to target Kafka");
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
                logger.LogError("Kafka producer error: {Code} - {Reason}", error.Code, error.Reason);
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

                    logger.LogDebug("Message sent to target Kafka topic {Topic} at offset {Offset}",
                        targetKafka.TopicName, deliveryResult.Offset);
                }
                catch (ProduceException<Null, string> ex)
                {
                    failCount++;
                    logger.LogError(ex, "Failed to send message to target Kafka: {Error}", ex.Error.Reason);
                }
            }

            logger.LogInformation(
                "Successfully sent {SuccessCount}/{TotalCount} messages to target Kafka topic: {Topic}",
                successCount, kafkaMessages.Count, targetKafka.TopicName);

            if (failCount > 0)
            {
                logger.LogWarning("Failed to send {FailCount} messages", failCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending to target Kafka");
            throw;
        }
    }
}