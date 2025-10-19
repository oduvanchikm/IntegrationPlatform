namespace IntegrationPlatform.Engine.Applications.Interfaces;

interface IKafkaMessageHandler
{
    Task HandleMessageAsync(string json);
}