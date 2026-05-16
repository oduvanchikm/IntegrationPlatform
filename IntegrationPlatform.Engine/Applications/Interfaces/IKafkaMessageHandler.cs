namespace IntegrationPlatform.Engine.Applications.Interfaces;

public interface IKafkaMessageHandler
{
    Task HandleMessageAsync(string json);
}