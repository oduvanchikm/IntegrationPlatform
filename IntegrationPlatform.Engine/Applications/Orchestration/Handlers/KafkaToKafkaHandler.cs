using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public class KafkaToKafkaHandler(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private async Task<List<string>> ConsumerFromSourceKafkaAsync(DataInterface dataInterface, int batchSize = 100)
    {
        if (dataInterface is not KafkaInterface kafkaInterface)
        {
            _logger.LogError("Source interface is not Kafka Interface");
            return new List<string>();
        }

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
            var timeout = TimeSpan.FromSeconds(30);
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

    private async Task SendToTargetKafkaAsync(DataInterface dataInterface, List<string> kafkaMessages)
    {
        if (dataInterface is not KafkaInterface targetKafka)
        {
            _logger.LogError("Target interface is not KafkaInterface");
            return;
        }

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

    public async Task ExecuteAsync(DataInterface publicationInterface, DataInterface subscriptionInterface)
    {
        _logger.LogInformation("KafkaToKafkaHandler.ExecuteAsync");

        var message = await ConsumerFromSourceKafkaAsync(publicationInterface, batchSize: 50);

        if (!message.Any())
        {
            _logger.LogError("KafkaToKafkaHandler.ExecuteAsync: No message found");
            return;
        }

        await SendToTargetKafkaAsync(subscriptionInterface, message);

        _logger.LogInformation("KafkaToKafkaHandler.ExecuteAsync");
    }
}