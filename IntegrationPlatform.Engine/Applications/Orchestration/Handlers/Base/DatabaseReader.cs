using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class DatabaseReader(ILogger<DatabaseReader> logger)
{
    public async Task ReadFromDatabaseAsync(DatabaseInterface apiInterface, List<string> messages)
    {
    }
}