using Confluent.Kafka;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Metrics;
using Prometheus;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class KafkaWriter(ILogger<KafkaWriter> logger)
{
    private static readonly Counter MessagesProduced = Prometheus.Metrics
        .CreateCounter("engine_kafka_messages_produced_total", 
            "Total messages produced to Kafka",
            new CounterConfiguration { LabelNames = new[] { "topic" } });
    
    private static readonly Counter ProduceErrors = Prometheus.Metrics
        .CreateCounter("engine_kafka_produce_errors_total", 
            "Total produce errors",
            new CounterConfiguration { LabelNames = new[] { "topic", "error_type" } });
    
    public async Task WriteToKafkaAsync(KafkaInterface targetKafka, List<string> messages)
    {
        logger.LogInformation("WriteToKafka called: {Count} messages to {Topic}@{Servers}", 
            messages.Count, targetKafka.TopicName, targetKafka.BootstrapServers);
        
        if (!messages.Any())
        {
            logger.LogDebug("No messages to send to target Kafka");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = targetKafka.BootstrapServers,
            LingerMs = 5,
            BatchSize = 16384,
            MessageTimeoutMs = 30000,
            SocketTimeoutMs = 30000,
            Debug = "broker,topic,msg" 
        };

        if (!string.IsNullOrEmpty(targetKafka.Username))
        {
            config.SaslUsername = targetKafka.Username;
            config.SaslPassword = targetKafka.Password;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
        }

        using var producer = new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, error) =>
            {
                logger.LogError("Kafka producer error: {Code} - {Reason}", error.Code, error.Reason);
                ProduceErrors.WithLabels(targetKafka.TopicName, "producer_error").Inc();
                EngineMetrics.ProcessingErrors.WithLabels("kafka_writer", "producer_error").Inc();
            })
            .SetLogHandler((_, logMessage) =>
            {
                logger.LogDebug("Kafka Producer Log: {Level} {Message}", logMessage.Level, logMessage.Message);
            })
            .Build();

        var successCount = 0;
        var failCount = 0;

        try
        {
            foreach (var message in messages)
            {
                try
                {
                    logger.LogDebug("Producing message: {Preview}", 
                        message.Length > 100 ? message[..100] + "..." : message);
                    
                    var kafkaMessage = new Message<Null, string>
                    {
                        Value = message,
                        Timestamp = new Timestamp(DateTime.UtcNow)
                    };

                    var deliveryResult = await producer.ProduceAsync(targetKafka.TopicName, kafkaMessage);
                    successCount++;
                    
                    MessagesProduced.WithLabels(targetKafka.TopicName).Inc();
                    
                    logger.LogDebug("Message sent to target Kafka topic {Topic} at offset {Offset}, partition {Partition}",
                        targetKafka.TopicName, deliveryResult.Offset, deliveryResult.Partition);
                }
                catch (ProduceException<Null, string> ex)
                {
                    failCount++;
                    logger.LogError(ex, "Failed to send message to target Kafka: {Error}", ex.Error.Reason);
                    ProduceErrors.WithLabels(targetKafka.TopicName, "produce_error").Inc();
                    EngineMetrics.ProcessingErrors.WithLabels("kafka_writer", "produce_error").Inc();
                }
            }

            var flushResult = producer.Flush(TimeSpan.FromSeconds(10));
            logger.LogDebug("Producer flush completed: {Remaining}", flushResult);

            if (successCount > 0)
            {
                logger.LogInformation("Successfully sent {SuccessCount}/{TotalCount} messages to {Topic}",
                    successCount, messages.Count, targetKafka.TopicName);
            }

            if (failCount > 0)
            {
                logger.LogWarning("⚠ Failed to send {FailCount} messages to {Topic}", failCount, targetKafka.TopicName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending to target Kafka");
            ProduceErrors.WithLabels(targetKafka.TopicName, "unexpected_error").Inc();
            throw;
        }
    }
}