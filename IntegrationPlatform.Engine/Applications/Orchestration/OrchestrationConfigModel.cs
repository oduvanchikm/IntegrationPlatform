using System.Text.Json.Serialization;
using IntegrationPlatform.Common.Enums;

namespace IntegrationPlatform.Engine.Applications.Orchestration;

public class OrchestrationConfigModel
{
    public int Id { get; set; }
    public int InterfaceSubscriptionId { get; set; }
    public int InterfacePublicationId { get; set; }
    public string IntegrationPattern { get; set; }
    public string ScheduleCron { get; set; }
}