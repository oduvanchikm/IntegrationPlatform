using Confluent.Kafka;
using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class KafkaReader(ILogger<KafkaReader> logger)
{
    public async Task ReadFromKafkaAsync(KafkaInterface kafkaInterface, Func<string, Task> onMessage,
        CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaInterface.BootstrapServers,
            GroupId = $"kafka-streaming-{kafkaInterface.Id}-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 30000,
            MaxPollIntervalMs = 300000,
            EnablePartitionEof = false,
            Debug = "consumer,cgrp,topic,fetch" 
        };

        if (!string.IsNullOrEmpty(kafkaInterface.Username))
        {
            config.SaslUsername = kafkaInterface.Username;
            config.SaslPassword = kafkaInterface.Password;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
        }

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(kafkaInterface.TopicName);
        logger.LogInformation("Streaming consumer subscribed to topic {Topic} with group {GroupId}", 
            kafkaInterface.TopicName, config.GroupId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? consumeResult = null;
                try
                {
                    try
                    {
                        consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                    }
                    catch (ConsumeException ex) when (ex.Error.IsLocalError && ex.Error.Code == ErrorCode.Local_TimedOut)
                    {
                        logger.LogDebug("⏱ Consume timeout - no new messages");
                        continue;
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex, "❌ Consume error: {Reason}", ex.Error.Reason);
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }
                    
                    if (consumeResult?.Message?.Value != null)
                    {
                        logger.LogInformation("📥 MESSAGE RECEIVED from {Topic} at offset {Offset}", 
                            kafkaInterface.TopicName, consumeResult.Offset.Value);
                        
                        try
                        {
                            await onMessage(consumeResult.Message.Value);
                            consumer.Commit(consumeResult);
                            logger.LogDebug("✅ Message committed at offset {Offset}", consumeResult.Offset.Value);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "💥 Error processing message at offset {Offset}", consumeResult.Offset.Value);
                        }
                    }
                    else
                    {
                        logger.LogDebug("🤷 Consume returned empty result");
                    }
                }
                catch (ConsumeException e)
                {
                    logger.LogError(e, "Error consuming from Kafka: {Reason}", e.Error.Reason);
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Streaming cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in streaming");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        finally
        {
            consumer.Close();
            logger.LogInformation("Streaming consumer closed for topic {Topic}", kafkaInterface.TopicName);
        }
    }
}